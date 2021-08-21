module FSharpCode.Account
open System.Net.Mail
open FSharpPlus
open System
open HOG.Database.Client open HOG.Database.Client.hogRequests
let validate_password (pass:string) success fail =
    if length pass > 8
        && pass |> exists Char.IsUpper
        && pass |> exists Char.IsDigit
        && pass |> exists Char.IsLower
        && pass |> exists (Char.IsLetterOrDigit >> not)
    then
//        printfn "good pas %A" pass
        success pass
    else
//        printfn "bad pass %A %A" pass (length pass > 8 , pass |> exists Char.IsUpper , pass |> exists Char.IsDigit , pass |> exists Char.IsLower , pass |> exists Char.IsSymbol)
                                    
        fail ()
let validate_email email success fail =
    try 
        let addr = (new MailAddress(email)).Address
        if addr = email then success email else fail () 
    with | _ -> fail ()
//asd!@#ASD091023
let validate_username (name:string) success fail =
    if length name > 3 && length name < 17 then success name else fail ()
let validate_login email password success fail_email fail_password =
    validate_email email (fun e -> validate_password password (success e) fail_password) fail_email
let validate_register username email password success fail_username fail_email fail_password =
    validate_username username (fun u -> validate_email email (fun e -> validate_password password (success u e) fail_password) fail_email) fail_username
let login (http:Lazy<HOGHTTPClient>) email password k_begin k_acc_not_found k_acc_not_activated_in_time k_login_success k_email_fail k_password_fail k_end =
    validate_login
        email
        password 
        (fun email password ->
            k_begin ()
            async{ 
                let temp_acc = snd <| AuthEd25519.AuthEd25519.load_acc_from_details "be5d9f66cf2ebed67f5a3c68ce69bd07a1ecad2b88db7cfaf5ac63aba8eaf6fe" "hog" email password
                match! (http.Value.get_one_name temp_acc.pub.string >>= (fun x -> http.Value.get_one_email temp_acc.pub.string |>> tuple2 x) ) with
                | [],_ | _,[] -> k_acc_not_found ()
                | (username::_),(email::_) when not email.activated && (DateTime.Now - email.timestamp).TotalHours > 24. -> k_acc_not_activated_in_time (DateTime.Now - email.timestamp)
                | (username::_),(email::_) -> do! k_login_success temp_acc username email
                k_end()
            } |> Async.StartAsTask |> ignore
        )
        (k_email_fail >> k_end)
        (k_password_fail >> k_end)
let login_print print_debug (http:Lazy<HOGHTTPClient>) email password k_login_success k_end =
    login http email password
        (fun _ -> print_debug "Logging in... Please wait...")
        (fun _ -> print_debug "Login Failed! Account not found!")
        (fun _ -> print_debug "Login Failed! You must activate your email. Check your inbox!")
        (fun acc username email ->
            k_login_success acc username email (fun _ -> print_debug <| sprintf "Login Successful! Welcome %s! %s" username.name (if not email.activated then "Please don't forget to activate your email!" else ""))
        )
        (fun _ -> print_debug "Email parsing error. Please use a different email." )
        (fun _ -> print_debug "Password must be >8 characters and contain uppercase, lowercase, digits, and symbols." )
        k_end
        
let register
    (http:Lazy<HOGHTTPClient>)
    username
    email
    password
    password_confirm
    news
    k_begin
    k_success
    k_fail_email_register
    k_fail_username_register
    k_fail_bad_username
    k_fail_bad_email
    k_fail_bad_password
    k_fail_passwords_mismatch
    k_end
    =
        k_begin()
        if password <> password_confirm then k_fail_passwords_mismatch ()
        else
        validate_register username email password
            (fun username email password ->
                printfn "1"
                async {
                    
                    let temp_acc = snd <| AuthEd25519.AuthEd25519.load_acc_from_details "be5d9f66cf2ebed67f5a3c68ce69bd07a1ecad2b88db7cfaf5ac63aba8eaf6fe" "hog" email password
                    let! res = http.Value.insert_name {pub = temp_acc.pub.string; name = username}
                    printfn "1.5 %s" res
                    let! ({name = username_acc_db}::_) = http.Value.get_one_name temp_acc.pub.string
                    printfn "2"
                    if username = username_acc_db then  
                        match! http.Value.insert_email {EmailQuery.email = email; pub = temp_acc.pub.string} with
                        | Ok msg ->
                            printfn "3"
                            do! http.Value.update_newsletter {pub = temp_acc.pub.string; newsletter = news}
                            do! k_success temp_acc username 
                        | Error e -> k_fail_email_register e
                    else k_fail_username_register ()
                    k_end()
                } |> Async.StartAsTask |> ignore
            )
            (k_fail_bad_username >> k_end)
            (k_fail_bad_email >> k_end)
            (k_fail_bad_password >> k_end)
            
        () 