module FSharpCode.Main
open System
open System.Collections.Generic
open System.Diagnostics
open System.Net
open System.Net.Security
open System.Runtime.CompilerServices
open System.Security.Cryptography.X509Certificates
open System.Text
open System.Threading.Tasks
open AuthEd25519
open EnginePrime.EnginePrime
open EnginePrime.EnginePrime.Networking
open FSharp.Control.Tasks
open FSharp.Data
open FSharpCode
open FSharpCode.Exts
open EnginePrime
open FSharpCode
open FSharpCode.Shop.Equipment
open FSharpCode.Shop.Shop
open FSharpPlus.Control
open FSharpPlus.Internals
open Godot
open HOG.Database.Client
open Ionic.Zip
open MBrace.FsPickler
open Model
open FSharpx.Collections
open EnginePrime.EnginePrime
open FSharpCode.Model
open System.IO
open FSharpPlus
open Shop
open Input
open System.Threading
open EnginePrime.GameFramework
open EnginePrime.GameLogic
open Controls
open HOG.Tools open HOG.Tools.Physics open HOG.Tools.Anim open Cooldown open SiaSkynet
                                                                        open Timer open HOG.Tools.Extras


open HOG.Database.Client.hogRequests
open HOG.Tezos.Client

type MainFs() as this =
    inherit Node2D()
//    let url = "ws://ivan.amitai.ga:8080/lobby"

    let enable_p2p = false//OS.GetName() <> "HTML5"
    let url = "ws://51.15.109.210:8010/lobby"
    let url = "ws://recklessgame.net:8020/lobby"
    let client = lazy(new WebSocketClient'(url) :> Networking.Client)   
    
    let n = this.get_node<Node2D>
    let game_result_screen = n"game_result_screen"
    
    let gladiators = this.get_node_children<Node2D> "arena1/gladiators"
    let shockwave_scene = ResourceLoader.Load<PackedScene>"res://scenes/vfx/shockwave.tscn"
    let shop_item_scene = ResourceLoader.Load<PackedScene>"res://scenes/shop/shop_item.tscn"
    let gladiator_scene = ResourceLoader.Load<PackedScene>"res://scenes/gladiator.tscn"
    let flying_limb_scene = ResourceLoader.Load<PackedScene>"res://scenes/flying_limb.tscn"
    let throwing_axe_scene = ResourceLoader.Load<PackedScene>"res://scenes/throwing_axe.tscn"
    let blood_spot_scene = ResourceLoader.Load<PackedScene>"res://scenes/blood_spot.tscn"
    
    let user_box_scene = ResourceLoader.Load<PackedScene>"res://scenes/ui/user_box.tscn"
    
    let db = DB()
    
    let friends_list = []

    let mutable settings = Settings.load
    let mutable listening_for_input : Button list = []
    let mutable visible_menu = []
    
    let game_mode_option_button = lazy ( this.n<OptionButton>"start_menu/btns/btn_mode")
    let online_local_option_button = lazy ( this.n<OptionButton>"start_menu/btns/btn_online_local")
    let rec controller =
        let player_pos name = this.get_node_children'<Node2D> "arena1/gladiators" |> tryFind (fun n -> n.get_text "name" = name) |> option (fun p -> Vec.nondeterministically p.Position.x p.Position.y) Vec.Zero 
        let player_pos_i i = this.get_node_children'<Node2D> "arena1/gladiators" |> tryItem i |> option (fun p -> Vec.nondeterministically p.Position.x p.Position.y) Vec.zero
        let mousepos () = this.GetGlobalMousePosition() |> fun v -> Vec.nondeterministically v.x v.y
        Controller.Controller (
         (function | true -> [db.user_name, []] | false -> settings.local_play_names |> List.map (fun n -> (fst <| fst n), [])),
         (function
            | true -> fun gs ->
                let mk_inp = match gs with Some(Left _) -> Left | _ -> Right 
                let esc_menu = (this.get_node<Control> "settings").Value
                let screen_center = this.GetViewportRect().Size/2.f

                let mousepos = mousepos()
                let direction =
                    function
                    | Mouse -> (player_pos db.user_name).direction_to mousepos
                    | LeftJoystickWASD -> (left_joystick_direction 0 + keyboard_direction "w" "s" "a" "d").normalized' 
                    | RightJoystickArrows -> (right_joystick_direction 0 + keyboard_direction "up" "down" "left" "right").normalized'
                [
                    mk_inp ({
                        ai = 0
                        is_charge_pressed = not esc_menu.Visible && exists (button_pressed 0) settings.controls.online_controls.throw 
                        is_run_pressed = not esc_menu.Visible && exists (button_pressed 0) settings.controls.online_controls.dash
                        emote =
                            match () with
                            | _ when keyboard_button_pressed Godot.KeyList.Key1 -> 1uy
                            | _ when keyboard_button_pressed Godot.KeyList.Key2 -> 2uy
                            | _ when keyboard_button_pressed Godot.KeyList.Key3 -> 3uy
                            | _ when keyboard_button_pressed Godot.KeyList.Key4 -> 4uy
                            | _ -> 0uy
                        move_dir = direction settings.controls.online_controls.move
                        aim_dir = direction settings.controls.online_controls.aim
                        dash_dir = direction settings.controls.online_controls.dashdir
                    })
                ]
            | false -> fun gs ->
                let mk_inp = match gs with Some(Left _) -> Left | _ -> Right 
                let mousepos = mousepos()
                let direction i = function | Mouse -> (player_pos <| (fst << fst) settings.local_play_names.[i]).direction_to mousepos | LeftJoystickWASD -> (left_joystick_direction i + keyboard_direction "w" "s" "a" "d").normalized'  | RightJoystickArrows -> (right_joystick_direction i + keyboard_direction "up" "down" "left" "right").normalized'
                settings.controls.local_controls
                |> Seq.zip [0..3]
                |> List.ofSeq
                |> List.map (fun (i, c) ->
                    mk_inp ({
                        ai = settings.controls.local_controls.[i].ai
                        is_charge_pressed = exists (button_pressed i) c.throw
                        is_run_pressed = exists (button_pressed i) c.dash
                        emote = 
                            match () with
                            | _ when joy_button_pressed Godot.JoystickList.DpadUp i -> 1uy
                            | _ when joy_button_pressed Godot.JoystickList.DpadRight i -> 2uy
                            | _ when joy_button_pressed Godot.JoystickList.DpadDown i -> 3uy
                            | _ when joy_button_pressed Godot.JoystickList.DpadLeft i -> 4uy
                            | _ -> 0uy
                        move_dir = direction i settings.controls.local_controls.[i].move
                        aim_dir = direction i settings.controls.local_controls.[i].aim
                        dash_dir = direction i settings.controls.local_controls.[i].dashdir
                    })) 
                
         ))
    let mutable tournament_index = -1
//    let mutable shop = Task.Run (fun _ -> {items=[];game_money = -1; real_money = -1;category=Head; equipment=(Equipment.basic) :: (["Green"; "Red"; "Blue"; "Yellow"] |> List.map (fun color -> {Equipment.basic with head = {Equipment.basic.head with name = color ++ " Bandana"} }))})
    let hermes = Hermes.mk_hermes <| {Hermes.mk_basic_hermes_config "reckless" controller reckless_normal_or_tournament url (konst client.Value) with k_side_effects = Some this.side_effects}
    let db_url = "https://higher-order-games.net:8087/"
    let join_url = "https://higher-order-games.net:8087/join_match" 
    
//    let name = 
//        let randomStr (chars:string) = 
//            let charsLen = chars.Length
//            let random = System.Random()
//
//            fun len -> 
//                let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
//                new System.String(randomChars)
//        "Guest" ++ randomStr "0123456789" 5, []
    let identity () : Networking.Identity = (db.user_name, []), Verified hermes.acc.pub
    let mutable last_sent_match_result = None
//    let pia() = List.init 5 (fun i -> equipments.[i])
    let pia() = AuctionHouse.get_account (fun acc -> AuctionHouse.my_equipments |>> fun eq -> acc.pub_hash,eq)
    let ia_normal = Left(Timer.mk_timer 0 4, ())
    let ia_tournament = Right(2, (Timer.mk_timer 0 4, ())) 
    
    let mutable kill_js = fun () -> ()
        
    member this.n'<'a when 'a :> Node> s = (this.get_node<'a> s).Value  
    override this._Ready() =
        let start_info = ProcessStartInfo()
        start_info.UseShellExecute <- false
        start_info.CreateNoWindow <- true
        start_info.WindowStyle <- ProcessWindowStyle.Hidden
        start_info.FileName <- (if OS.GetName() = "Windows" then "tezos_server-win.exe" else "tezos_server-linux")
        let js = Process.Start(start_info)
        kill_js <- fun () -> js.Kill()
        do js.OutputDataReceived.Add(fun d -> printfn "js: %A" d.Data)
        do AuctionHouse.download_token_imgs |> Async.StartAsTask
        Task.Run (fun _ -> Serializers.pickle_serializer.round_trip (reckless_normal_or_tournament.start ia_normal DateTime.Now true Map.empty) |> ignore )
        printfn "os: %s" <| OS.GetName()
        WebClient().AsyncDownloadString(Uri <| http_proxy "https://api.better-call.dev/v1/bigmap/florencenet/115682/keys") |>> printfn "downloaded string %A" |> Async.RunSynchronously
//        Async.StartAsTask (skynet_upload_token_metadata "Token 1" "tok1" 0 false "/home/vankata/Downloads/gds_logo.png" ["/home/vankata/Downloads/gds_logo.png";"/home/vankata/Downloads/gds_logo.png"] |>> printfn "sia tm: %A")
//        AuctionHouse.propose_token 2005 [AuctionHouse.get_account (fun acc -> acc.pub_hash), 1000] "https://siasky.net/AAB-gLIu1N_wjFmDIjWUX4Ci3qfR00mU4alM3DEseJKECQ"
//            |>> printfn "proposal: %A" |> Async.StartImmediate
//        AuctionHouse.display this "ah" |> Async.StartAsTask
//        let name, acc = File.load_encrypted "user://acc.save" (db.user_name, hermes.acc)
//        db.user_name <- name
//        hermes.acc <- acc
        Equipment.load_equipments hermes.acc.pub.string
        let click_sfx =
            for n in this.get_all_of_type<Button>() do
                n.on_event_add "pressed" (fun _ -> match n.Name with | "buy" -> this.play_sfx "sfx_buy" | "equip" -> () | _ -> this.play_sfx "sfx_btn")
            for n in this.get_all_of_type<TextureButton>() do
                n.on_event_add "pressed" (fun _ -> match n.Name with | "buy" -> this.play_sfx "sfx_buy" | "equip" -> () | _ -> this.play_sfx "sfx_btn")
        client.Value.connect url
        let n = this.n'
        //let db_url = "https://localhost:8090/match_start"
        let initial_args () = match game_mode_option_button.Value.Selected with 1 -> ia_tournament | _ -> ia_normal
        
        this.child_on_events_add [
       
            ["game_result_screen/btn_hide"], "pressed", (fun _ ->  (n<Position2D>"game_result_screen/lerp_towards").Scale <- Vector2(0.f, 1069.f))
            ["game_result_screen/btn_show"], "pressed", (fun _ -> (n<Position2D>"game_result_screen/lerp_towards").Scale <- Vector2.Zero )
            ["game_result_screen/btn_play_again"], "pressed", (fun _ -> controller.enqueue_command [Command.Play (hermes.in_online_game.Value, (reckless_normal_or_tournament.names hermes.arena_model.Value).Length,pia(),(match hermes.arena_model with Some(Right _) -> ia_tournament | _ -> ia_normal), Some db_url)] )
            ["game_result_screen/btn_main_menu"], "pressed", (fun _ -> let _ = printfn "LEAVE" in controller.enqueue_command [Command.LeaveToMainMenu] )

        ]
        this.child_on_events_add [
            ["start_menu/btns/btn_fight"; ], "pressed", (fun _ ->
                let x = 1920.f/2.f
                controller.enqueue_command [Command.Play ((match online_local_option_button.Value.Selected with 0 -> true | _ -> false), int (this.get_node<SpinBox>"start_menu/btns/arena_size").Value.Value, pia(), initial_args(), Some db_url)]
                this.set_meta "menu" "interpolate_towards" (konst <| Vector2(x, -60.f)) |> ignore
                )
            ["start_menu/btns2/btn_duel"], "pressed", (fun _ ->
                let x = 1920.f/2.f
                let size = int (this.n<SpinBox> "start_menu/btns2/size").Value
                controller.enqueue_command [Command.Play((this.n<CheckButton>"start_menu/btns2/online").Pressed, size, pia(), ia_normal, Some db_url)]
                this.set_meta "menu" "interpolate_towards" (konst <| Vector2(x, -60.f)) |> ignore
                )
            ["start_menu/btns2/btn_tournament"], "pressed", (fun _ ->
                let x = 1920.f/2.f
                let size = (this.n<SpinBox> "start_menu/btns2/size").Value
                controller.enqueue_command [Command.Play((this.n<CheckButton>"start_menu/btns2/online").Pressed, int size, pia(), ia_tournament, Some db_url)]
                this.set_meta "menu" "interpolate_towards" (konst <| Vector2(x, -60.f)) |> ignore
                )
        ]
        
        let _ready_login_register =
            let text path = (this.get_node<LineEdit> ("login_register/vbox/grid/" + path)).Value.Text
            let clean path = for p in path do (this.get_node<LineEdit> ("login_register/vbox/grid/" + p)).Value.Text <- ""
            let print_debug msg = this.set_text ["login_register/vbox/debug", msg]
            this.child_on_events_add [
                ["login_register/vbox/btns/btn_login"], "pressed", (fun _ ->
                            let username = text "username"
                            Account.validate_username username (fun username ->
                                async {
                                    let! acc = AuctionHouse.login (text "password")
                                    print_debug (sprintf "Welcome, %s" username)
                                    db.user_name <- username
                                }|> Async.StartImmediate
                                ) (fun e -> printfn "Please use a name between 3 and 16 characters.")
//                        this.set_text["login_register/vbox/label", "Login"]
//                        if (this.n<Control>"login_register/vbox/grid/password_confirm").Visible
//                        then this.set_visibility ["login_register/vbox/grid/password_confirm", false; "login_register/vbox/grid/username", false; "login_register/vbox/grid/l_password_confirm", false; "login_register/vbox/grid/l_username", false; "login_register/vbox/hbox/news", false; "login_register/vbox/hbox/tos", false]
//                        else
//                            let email = text "email"
//                            let password = text "password"
//                            let remember = (this.n<CheckBox>"login_register/vbox/hbox/remember").Pressed
//                            Account.login_print print_debug http email password
//                                (fun acc username email print_success ->
//                                    async {
//                                        hermes.acc <- acc
//                                        db.user_name <- username.name
//                                        load_equipments hermes.acc.pub.string
//                                        if remember then File.change_encrypted "user://acc.save" (db.user_name, hermes.acc)
//                                        do! Relationships.reload_friendships hermes.acc.pub.string
//                                        print_success()
//                                    } 
//                                )
//                                (fun _ -> clean ["email"; "password"])
                            
                        )
                ["login_register/vbox/btns/btn_register"], "pressed", (fun _ ->
                        this.set_text["login_register/vbox/label", "Register"]
                        if not (this.n<Control>"login_register/vbox/grid/password_confirm").Visible
                        then this.set_visibility ["login_register/vbox/grid/password_confirm", true; "login_register/vbox/grid/username", true; "login_register/vbox/grid/l_password_confirm", true; "login_register/vbox/grid/l_username", true; "login_register/vbox/hbox/news", true; "login_register/vbox/hbox/tos", true]
                        else
                            let username = text "username"
                            let email = text "email"
                            let password = text "password"
                            let password_confirm = text "password_confirm" 
                            let remember = (this.n<CheckBox>"login_register/vbox/hbox/remember").Pressed
                            let news = (this.n<CheckBox>"login_register/vbox/hbox/news").Pressed
                            Account.register http username email password password_confirm news
                                (fun _ -> print_debug "Registering... Please wait..")
                                (fun temp_acc username ->
                                    printfn "here"
                                    async{ 
                                        hermes.acc <- temp_acc
                                        db.user_name <- username
                                        load_equipments hermes.acc.pub.string
                                        do! Relationships.reload_friendships hermes.acc.pub.string
                                        if remember then File.change_encrypted "user://acc.save" (db.user_name, hermes.acc) else ()
                                        Printf.kprintf print_debug "Registration Successful! Welcome %s! You have 24 hours to activate your email. Check your inbox and spam folders." username
                                    }
                                )      
                                (sprintf "Registration failed! %s!" >> print_debug)
                                (fun _ -> print_debug "Registration Failed! Please try again with a different username!")
                                (fun _ -> print_debug "Username too short. Please use a different one.")
                                (fun _ -> print_debug "Email parsing error. Please use a different email.")
                                (fun _ -> print_debug "Password must be >8 characters and contains uppercase, lower, digits, and symbols.")
                                (fun _ -> print_debug "Error:Password and password confirmation not the same!")
                                (fun _ -> clean ["username"; "email"; "password"; "password_confirm"])
                            
                        )
            ]
        let display_ah () = AuctionHouse.display this "ah" |> Async.StartImmediate
        //-----Shop---------
        let _ready_shop =
            this.child_on_events_add[
//                ["shop/categories/head"], "pressed", (fun _ -> Shop.change_category Head)
//                ["shop/categories/arms"], "pressed", (fun _ -> Shop.change_category Arms)
//                ["shop/categories/legs"], "pressed", (fun _ -> Shop.change_category Legs)
//                ["shop/categories/torso"], "pressed", (fun _ -> Shop.change_category Torso)
//                ["shop/categories/weapon"], "pressed", (fun _ -> Shop.change_category Weapon)
                ["ah/categories/head";"ah/categories/arms";"ah/categories/legs";"ah/categories/torso";"ah/categories/weapon";"ah/categories/box";"ah/section/ah"; "ah/section/inventory"; "ah/section/proposals"], "pressed", display_ah
                ["ah/search"], "changed_text", display_ah
            ]
        //-----Settings-----
        let _ready_settings =
            printfn "loaded settings %A" settings
            AudioServer.SetBusVolumeDb (AudioServer.GetBusIndex("Master"), - 80.f * (1.f - (settings.audio.Item "master" |> snd)))
            AudioServer.SetBusMute (AudioServer.GetBusIndex("Master"), settings.audio.Item "master" |> fst |> not)
            for [l;m;v] in ((this.get_node_children'<Node>"settings/bg/vbox/audio") |> fun xs -> List.splitInto (xs.Length / 3) xs) do
                let txt = l.Name |> String.skip 2
                printfn "setting volume for %A" txt
                (v:?>Slider).Value <- float (settings.audio.Item txt |> snd) * 100.
                (m :?> CheckButton).Pressed <- settings.audio.Item txt |> fst
            let controls = this.n "settings/bg/vbox/controls"
            for i, n in List.indexed settings.local_play_names do controls.set_text [sprintf "local%d/player" (i+1), fst <| fst n]
            let set_option (n:Node) s v = (n.n<OptionButton> s).Select v 
            let set_controls (n:Node) (cs:ControlScheme) =
                printfn "settings controls %A : %A" n.Name cs
                let set_option = set_option n
                set_option "ai" cs.ai
                set_option "o_move" <| analog_style_to_int cs.move
                set_option "o_aim" <| analog_style_to_int cs.aim
                set_option "o_dashdir" <| analog_style_to_int cs.dashdir
                
                let btns_throw = n.get_node_children'<Button> "btns_throw"
                Seq.iter2 (fun (n:Button) (i:InputKey) -> n.Text <- InputKey.string i) btns_throw cs.throw
                let btns_dash = n.get_node_children'<Button> "btns_dash"
                Seq.iter2 (fun (n:Button) (i:InputKey) -> n.Text <- InputKey.string i) btns_dash cs.dash
                for b in btns_throw do b.on_event_add "pressed" (fun _ -> listening_for_input <- [b])
                for b in btns_dash do b.on_event_add "pressed" (fun _ -> listening_for_input <- [b])
            set_controls (controls.n "online") settings.controls.online_controls
            set_controls (controls.n "local1") settings.controls.local_controls.[0]
            set_controls (controls.n "local2") settings.controls.local_controls.[1]
            set_controls (controls.n "local3") settings.controls.local_controls.[2]
            set_controls (controls.n "local4") settings.controls.local_controls.[3]
            
            this.child_on_events_add [
                
                ["settings/bg/vbox/btns/save"], "pressed", (fun _ -> Settings.save settings )
            ]
        let _menu =
                let toggle path = visible_menu <- match visible_menu with | p when p = path -> [] | _ -> path 
                this.child_on_events_add [
                    ["menu/hbox/btn_settings"; "settings/bg/vbox/btns/close"], "pressed", (fun _ -> toggle ["settings"] )
//                    ["menu/hbox/btn_shop"], "pressed", (fun _ -> Shop.reload_shop hermes.acc.pub.string |> Async.StartAsTask |> ignore)
                    ["menu/hbox/btn_shop"], "pressed", (fun _ -> AuctionHouse.display this "ah" |> Async.StartImmediate |> ignore)
                    ["menu/hbox/btn_shop"; "ah/btn_close"], "pressed", (fun _ -> toggle ["ah"] )
                    ["menu/hbox/btn_login_register"; "login_register/vbox/btns/btn_close"], "pressed", (fun _ -> toggle ["login_register"] )
                    ["menu/hbox/btn_leave_arena"], "pressed", (fun _ -> let _ = printfn "LEAVE" in controller.enqueue_command [Command.LeaveToMainMenu] )
                    ["menu/hbox/btn_exit_game"], "pressed", (fun _ -> let _ = controller.enqueue_command [Command.LeaveToMainMenu] in this.GetTree().Notification(MainLoop.NotificationWmQuitRequest))
                ]
        let _friends_list =
            this.child_on_events_add [
                ["start_menu/player_list/scroll/groups/send_friend_request/hbox/btn_send"], "pressed", (fun _ ->
                        let name = this.get_text "start_menu/player_list/scroll/groups/send_friend_request/hbox/label"
                        printfn "trying to add friend %A" name
                        async {
                            let! pub = GenericRequests.postAsync http.Value.http_client (url + "get_pub") {Name.name = name; pub = ""}
                            let! _ = http.Value.insert_friendship (hermes.acc.pub.string, Some name, "")
                            printfn "here"
                        } |> Async.StartAsTask
                        ()
                    )
            ]
        let chat =
            this.child_on_events_add [
                ["start_menu/chat/line_edit/btn"], "pressed", (fun _ ->
                    let t = this.get_text "start_menu/chat/line_edit"
                    if t <> "" then do hermes.send_msg [ClientToServer.Message t]
                    this.set_text ["start_menu/chat/line_edit", ""]
                    )
            ]
        let _invade_spectate =
            this.child_on_events_add[
                ["arena1/btn_invade"], "pressed", (fun _ -> match hermes.arena_model with None -> () | Some a -> (match reckless_normal_or_tournament.names a with ((name, _), Verified pub)::_ -> controller.enqueue_command[LeaveToMainMenu; Join(IdentityQuery.ByIdentity(name, None, pub),pia().[0], Some join_url)  ] | _ -> ()))
            ]
        let _scroll_tournament =
            this.child_on_events_add [
                ["arena1/arrows/left"], "pressed", (fun _ -> tournament_index <- match hermes.arena_model with Some (Right t) -> (if tournament_index = 0 then t.games.Length - 1 else tournament_index - 1) | _ -> 0)
                ["arena1/arrows/right"], "pressed", (fun _ -> tournament_index <- match hermes.arena_model with Some (Right t) -> (if tournament_index = t.games.Length - 1 then 0 else tournament_index + 1) | _ -> 0)
            ]
        this.child_on_events_add [
            ["arena1/btn_tostring"] , "pressed", (fun _ -> (this.n<TextEdit>"arena1/popup_panel/text_edit").Text <- string hermes.game_state )
        ]
        
        
        ()

    override this._Process dt =
        let delta_fixed = fix64.nondeterministically dt
        let arena_prev = hermes.arena_model.IsSome
        hermes.lockstep_tick delta_fixed //with e -> printfn "error %A" e
        tournament_index <- match tournament_index, hermes.arena_view with -1, Some(Right t) -> t.index_of_game_with_player (identity()) tournament_index | n, Some(Right _) -> n | _ -> -1
        let game_state = hermes.game_state
        
        
        last_sent_match_result <-
            match hermes.arena_model, hermes.game_result with
            | Some a, Some gr when last_sent_match_result <> Some(gr) && hermes.in_online_game |> Option.defaultValue false ->
                let match_res = gr
                let req = http.Value.match_awards{
                              MatchAwards.pub = hermes.acc.pub.string
                              timestamp = gr.timestamp
                              people = gr.names |> Map.keys |>> (snd >> (function Verified p -> p.string | _ -> "error")) |> List.ofSeq
                              awards = compute_match_awards gr.places match_res.names
                }
                Async.StartAsTask req 
                
                Some gr
            | _ -> last_sent_match_result
                
        this.View dt
        
        this.Audio dt
        
        //-------DebugDraw---------------       
        this.Update()
        
        if hermes.arena_view.IsSome && Input.IsActionJustPressed "debug"
        then do (this.get_node<RichTextLabel> "debug_layer/debug").Value.Text <- hermes.game_state.ToString() ++ " \n and interp: " ++ hermes.arena_view.ToString()

    override this._Input event =
        if event.IsActionPressed "enter" then do 
            hermes.send_msg [ClientToServer.Message(this.get_text "start_menu/chat/line_edit")]
            this.set_text ["start_menu/chat/line_edit", ""]
        listening_for_input <-
            match listening_for_input, event with
            | [btn], (:? InputEventKey as inp) when inp.Pressed -> let _ = btn.Text <- "k " ++ string (inp.Scancode |> int32 |> enum<KeyList>) in []
            | [btn], (:? InputEventMouseButton as inp)  when inp.Pressed -> let _ = btn.Text <- "m " ++ string (inp.ButtonIndex |> int32 |> enum<ButtonList>) in []
            | [btn], (:? InputEventJoypadButton as inp)  when inp.Pressed -> let _ = btn.Text <- "j " ++ string (inp.ButtonIndex |> int32 |> enum<JoystickList>) in []
            | x, _ -> x 
        ()


    member this.side_effects (se:Either<SideEffects, SideEffects []>) =
            
        let se = match se with | Left se -> se | Right se -> se.[clamp 0 se.Length (tournament_index%se.Length)]
        
        for i in [1..se.dashes] do gladiators.Value.[i].play_sfx "sfx_dash"
        for i in [1..se.axes_caught] do gladiators.Value.[i].play_sfx "sfx_catch"
        for l in [0..se.limbs_cut - 1] do gladiators.Value.[l].play_sfx "audio_slice"
        for t in [0..se.axes_thrown - 1] do gladiators.Value.[t].play_sfx "audio_throw"
        for ac in [0..se.axe_impacts - 1] do let _ = printfn "axe impact" in ((this.get_node_children<Node2D>"arena1/axes").Value.[ac].get_node<Node>"vpc/vp/axe/impact_sound").Value.Call("play") |> ignore
        for i in [0..se.limbs_picked_up-1] do gladiators.Value.[i].play_sfx "sfx_pickup_limb"
        for i in [0..se.axes_picked_up - 1] do gladiators.Value.[i].play_sfx "sfx_pickup_weapon"
        ()
    member this.Audio dt =
        if (this.n<Control>"settings").Visible then do
            AudioServer.SetBusVolumeDb (AudioServer.GetBusIndex("Master"), - 80.f * (1.f - (settings.audio.Item "master" |> snd)))
            AudioServer.SetBusMute (AudioServer.GetBusIndex("Master"), settings.audio.Item "master" |> fst |> not)
        
        
        for bus, (enabled, volume) in settings.audio |> Map.filter (fun k v -> k <> "master") |> Map.toList do
            AudioServer.SetBusVolumeDb (AudioServer.GetBusIndex(bus), - 40.f * (1.f - volume))
            AudioServer.SetBusMute (AudioServer.GetBusIndex(bus), not enabled)
//            GD.Print (AudioServer.GetBusVolumeDb (AudioServer.GetBusIndex(bus)))
        let anim_heartbeat = (this.n<AnimationPlayer>"arena1/heartbeat/anim")
        match hermes.arena_model with
        | Some arena ->
            let arena = snd <| match arena with Left a -> a | Right t -> t.game_with_player ((=)(identity())) tournament_index 
            let tension = 5.f - (arena.gladiators |> List.filter (fun g -> g.limbs.Length > 0)|> List.map (fun g -> g.limbs.Length) |> fun l -> if l.IsEmpty then 0.f else List.min l |> float32)
            if tension >= 2.f 
            then do
                anim_heartbeat.Play("heartbeat")
                anim_heartbeat.PlaybackSpeed <- tension/2.f
            else do
                anim_heartbeat.Stop()
        | _ ->  anim_heartbeat.Stop()
        monad' {
            let! arena = hermes.arena_model
            try
                let arena = snd <| match arena with Left a -> a | Right t -> t.game_with_player  ((=)(identity())) tournament_index
                match hermes.game_result with Some gr -> this.play_anim "anim_sfx" (if gr.winner |> Seq.contains (identity()) || not hermes.in_online_game.Value then "win" else "lose") | _ -> this.stop_anim "anim_sfx"
                for i in [0..length arena.axes-1] do (this.get_node_children'<Node2D> "arena1/axes").[i].play_sfx_if (arena.axes.[i].body.speed > fix64.Zero) "vpc/vp/axe/sfx_fly"
                for i, _ in Seq.indexed (Seq.zip arena.gladiators gladiators.Value) do
                    if arena.gladiators.[i].is_charging
                    then (gladiators.Value.[i].play_sfx_once "sfx_charge").PitchScale <- match arena.gladiators.[i].charge with | t -> 1.f + (Timer.completion t |> float32)/2.f | _ -> 1.f
                    else gladiators.Value.[i].stop_sfx "sfx_charge"
            with e -> ()
            ()
        } |> ignore
        ()
    member this.View dt =
        let game_state = hermes.game_state
        let n = this.n'
        hermes.use_interpolation <- settings.use_interpolation
        this.set_visibility [
            "start_menu", match game_state with | StartMenu _-> true | _ -> false
            "loading_screen", match game_state with | StartMenu((_,ns,_,_,_)::_) when not ns.IsEmpty -> true | _ -> false
            "arena1", match game_state with | InArena _ -> true | _ -> false
            "fps", settings.show_fps
        ]
        
        
        let _view_connection_status =
            this.set_text[ "connection_status", string <| (client.Value :?> WebSocketClient').GetConnectionStatus() ]
            this.set_visibility[ "connection_status", (client.Value).is_connected() |> not]
            
//        Performance.print_benchmark "arena view: " (fun _ -> 
        this.set_visibility [
            "arena1/arrows", (match hermes.arena_view with Some t -> (this.n<Control>"menu").RectPosition.y > -40.f && (match t with Right t -> t.games.Length > 1 | _ -> false) | _ -> false);
            "arena1/stage", (match hermes.arena_view with Some (Right _) -> (this.n<Control>"menu").RectPosition.y > -40.f | _ -> false)
        ]
        let _view_arena =
            monad' {
                let! gs = hermes.arena_model
                let countdown_timer, arena = match gs with Left a -> a | Right t -> t.game_with_player ((=)(identity())) tournament_index
                this.set_visibility[
                    "arena1/btn_invade", hermes.in_online_game |> Option.defaultValue false && reckless_normal_or_tournament.names gs |> List.contains (identity()) |> not && match hermes.arena_model with Some (Left _) -> true | _ -> false
                    "arena1/countdown_timer", Timer.completed countdown_timer |> not
                ]
//                let! arena = if not (this.n<CheckBox>"arena1/check_box").Pressed then hermes.arena_view else hermes.debug_game |> List.tryItem (hermes.debug_game.Length - 1 - int (this.n<SpinBox>"arena1/spin_box").Value) 
                if Input.IsActionJustPressed "debug" then do (this.n<TextEdit>"arena1/popup_panel/text_edit").Text <- string arena 
                this.set_text[
                    "arena1/stage", match hermes.arena_view with Some(Right t) -> sprintf "%s: %s" t.stage (reckless.names arena |>> (fst >> fst) |> String.concat " - ") | _ -> ""
                    "arena1/countdown_timer", match countdown_timer with t when Timer.completed t -> "" | Timer(t, t') when t >= fix64 3 -> "Fight!" | Timer(t, t') -> sprintf "%d" (t' - (t + fix64.Zero) |> int)
                ]
                init_and_process this gladiator_scene arena.id_gladiators "arena1/gladiators"
                    (fun g gs -> do AuctionHouse.display_char_equipments gs g.equipment)
                    (fun g (gs:Node2D) ->
                    gs.Position <- Vector2.from_vec g.body.position
                    gs.Position <- Vector2(gs.Position.x, gs.Position.y - 5.f)
                    let anim =
                        (match g.dash, g.is_charging with
                         | Cooldown.Active (d), _ -> "dash"
                         | _, true -> if g.has_axe_l then "swingl" else "swingr"
                         | _, _ -> if Body.velocity g.body |> Vec.length > fix64 0 then "walk" else "idle")
                    gs.n<AnimationPlayer>("vpc/vp/body/anim_body").Play anim
                    gs.n<AnimationPlayer>("vpc/vp/body/anim_body").PlaybackSpeed <- clamp 1.f 100.f (float32 (g.body.speed / Gladiator.base_speed + fract 5 5 + Timer.completion g.charge))
                    (gs.n<Node2D> "vpc/vp/body").XFlipDir (float32 (g.body.direction.x + g.charge_direction.x))
                    let n = gs.n
                    gs.set_visibility'[
                        n"vpc/vp/body/l_hand",     g.limbs |> List.exists (fst >> function | LH _ -> true | _ -> false)
                        n"vpc/vp/body/l_hand/axe", g.has_axe_l
                        n"vpc/vp/body/r_leg",      g.limbs |> List.exists (fst >> (=) RL)
                        n"vpc/vp/body/r_hand",     g.limbs |> List.exists (fst >> function | RH _ -> true | _ -> false)
                        n"vpc/vp/body/r_hand/axe", g.has_axe_r
                        n"vpc/vp/body/l_leg",      g.limbs |> List.exists (fst >> (=) LL)
                        n"vpc/vp/body/head",       g.limbs |> List.exists (fst >> (=) H)
                        n"vpc/vp/body",            true
                    ]
                    let n = g.identity

                    gs.set_emitting [
                        "blood_particles/head", g.limbs |> List.exists (fst >> (=) H) |> not
                        "blood_particles/l_hand", g.limbs |> List.exists (fst >> function | LH _ -> true | _ -> false) |> not
                        "blood_particles/r_hand", g.limbs |> List.exists (fst >> function | RH _ -> true | _ -> false) |> not
                        "blood_particles/l_leg", g.limbs |> List.exists (fst >> (=) LL) |> not
                        "blood_particles/r_leg", g.limbs |> List.exists (fst >> (=) RL) |> not
                        "charge_particles", g.is_charging
                        "invader_particles", g.invader.IsSome
                        "invader_particles2", g.invader.IsSome
                    ]

                    let arrow = gs.n<Node2D>"arrow" in let _ = arrow.Visible <- g.is_charging && g.charge_direction <> Vec.zero in let _ = arrow.LookAt (Vector2.from_vec (g.body.position + g.charge_direction * fix64 100))
                    match (gs.n<Node2D>"charge_particles") with | :? CPUParticles2D as p -> p.SpeedScale <- 0.5f + 10.f * float32 g.charge.completion | :? Particles2D as p -> p.SpeedScale <- 0.5f + 10.f * float32 g.charge.completion | _ -> ()
                    (gs.n<Node2D>"blood_particles").XFlipDir (float32 (g.body.direction.x + g.charge_direction.x))
                    if g.just_caught_axe_in_air then do
                         let shockwave = shockwave_scene.Instance() :?> Sprite in let _ = shockwave.GlobalPosition <- gs.GlobalPosition in gs.AddChild(shockwave)
                    if g.throw_speed_bonus > fix64 3000 then do
                        (this.n<Camera2D>"camera").Call("add_trauma", 0.2f) |> ignore
                    (gs.n<CPUParticles2D>"dust").Emitting <- g.body.speed > fract 15 10 * Gladiator.base_speed
                    (gs.n<Sprite>"vpc/vp/body").RotationDegrees <- 5.f * float32 (g.body.direction.x * g.body.speed/( fix64 2 * Gladiator.base_speed))
                    gs.set_text ["name", g.name]
                    (gs.n<Label> "name").Set("custom_colors/font_outline_modulate", if g.invader.IsSome || g.ai then Colors.Crimson else if g.name = db.user_name then Colors.SeaGreen else Colors.Black)
                    match g.speech with | 1 | 2 | 3 | 4 -> let _ = (gs.n<AnimationPlayer>"anim_speech").Play("pop") in  gs.set_text["speech", match g.speech with | 3 -> "Catch this!" | 2 -> "Chop! Chop! Chop!" | 4 -> "*Ha ha!*" | 1 -> "Greetings!" | _ -> "Error"] | _ -> ()
                    
                   )
                
                this.set_visibility ["arena1/invader", arena.gladiators |> List.exists (function {invader=Some(t)} when not (Timer.completed t) -> true | _ -> false)]   
                this.set_text ["arena1/invader", arena.gladiators |> List.choose (function {invader=Some((Timer(t', t'') as t))} when not (Timer.completed t) -> Some <| sprintf "Invader Approaching!\n %A" (int (t''-t'))  | _ -> None) |> List.tryHead |> Option.defaultValue ""]
                
                let overlay = (this.n<ColorRect>"camera/overlay")
                overlay.Color <- Color(overlay.Color.r, overlay.Color.g, overlay.Color.b, Mathf.lerp overlay.Color.a ((List.sumBy (fun g -> (float32 g.throw_speed_bonus)/((length>>float32) arena.gladiators * 100.f)) arena.gladiators / 50.f - 0.2f) |> clamp 0.f 1.f )  dt)
                   
         
                init_and_process this flying_limb_scene arena.elimbs "arena1/limbs"
                    (fun l (ls:Sprite) ->
                        ls.Texture <- arena.gladiators |> find (fun g -> g.identity = snd l.limb_type) |> fun g -> AuctionHouse.equipment g.equipment (match fst l.limb_type with LimbType.H -> Head | LL | RL ->Legs | RH _ | LH _ -> Arms) |> AuctionHouse.cached_token_metadata AuctionHouse.texture_path |> item (match fst l.limb_type with LL | LH _ -> 1 | _ -> 0) |> ResourceLoader.load_texture
                    )
                    (fun l (ls:Sprite) -> 
                        ls.Position <- Vector2.from_vec l.body.position
                        let flight_completion = l.flight_duration |> Timer.completion |> float32
                        
//                        ls.x_flip (float32 l.initial_velocity.normalized.x)
                        ls.RotationDegrees <-
                                let dir = if ls.FlipH then -1.f else 1.f
                                let end_goal = dir * (float32 <| l.initial_velocity.length * l.initial_velocity.x * l.initial_velocity.y * (l.flight_duration |> Timer.max_time)) % 217.f * 3.f 
                                flight_completion * end_goal
//                        ls.Rotate (Mathf.Deg2Rad(float32 l.body.direction.x * float32 l.body.speed * dt))
//                        if old_pos = Vector2(-1000.f, -1000.f) && old_pos <> ls.Position then do
                    )
                    
//                for ls in limb_sprites do
//                    let p_blood = ls.n<Particles2D> "blood_particles/blood"
//                    p_blood.Emitting <- ls.Visible
                    
                init_and_process this throwing_axe_scene arena.eaxes "arena1/axes"
                   (fun ax (axs:Node2D) ->
                    axs.set_texture["vpc/vp/axe", arena.gladiators |> find (fun g -> g.identity = ax.owner) |> fun g -> AuctionHouse.equipment g.equipment Weapon |> AuctionHouse.cached_token_metadata AuctionHouse.texture_path |> head |> ResourceLoader.load_texture]
                   )
                   (fun ax (axs:Node2D) -> 
                        axs.Position <- Vector2.from_vec ax.body.position
                   
                        let spr = (axs.n<Sprite>"vpc/vp/axe")
                        let flight_completion = ax.flight_duration |> Timer.completion |> float32
                        spr.y_flip (float32 ax.latest_direction.x)
                        spr.RotationDegrees <-
                                let dir = if spr.FlipV then -1.f else 1.f//spr.Scale.y
                                let end_goal =
                                    if ax.was_thrown
                                    then (ax.flight_duration |> Timer.max_time |> float32 |> round) * 3.f * 360.f * dir  + 131.f * dir + (180.f * Mathf.Clamp(-dir, 0.f, 1.f))
                                    else (float32 <| ax.initial_velocity.length * ax.initial_velocity.x * ax.initial_velocity.y * (ax.flight_duration |> Timer.max_time)) % 217.f * 3.f 
                                flight_completion * end_goal
                        spr.Set("show_after_images", not ax.can_be_picked_up && ax.was_thrown)
                        axs.set_shader_param [
                            "view_axe_with_shadow", "distance_from_ground", 0.1f - 0.1f * float32 ax.flight_duration.completion 
                            "view_axe_with_shadow", "shadow_alpha", if not ax.can_be_picked_up then 0.5f else 0.f 
                        ]
//                        if axs.get_meta "vpc" "thrown_by" |> Option.defaultValue ax.owner <> ax.owner then do spr.Texture <- skin ax.owner SkinAxe
                        axs.set_meta "vpc" "thrown_by" (konst (hash ax.owner))
                   )
                
                
                init_and_process this blood_spot_scene arena.blood_spots "arena1/blood_spots"
                    (fun (bs:BloodSpot) (bs_s:Node2D) -> bs_s.Position <- Vector2.from_vec bs.body.position)
                    (fun bs bs_s ->
                        bs_s.Scale <- ((float32 (bs.duration.completion + (bs.body.position.x%(fix64 10))/(fix64 100) ) )/1.f) * Vector2(1.f, 0.85f)
                        bs_s.RotationDegrees <- ((bs_s.Position.x + bs_s.Position.y) * 1000.f) % 360.f
                    )
                       
            } |> ignore
        ()
//        )
        
        //----------GameResultScreen-------
        let _view_game_result = 
            game_result_screen.Value.Visible <- match hermes.game_result with | Some _ -> true | _ -> false
            game_result_screen.Value.Position <- game_result_screen.Value.Position.LinearInterpolate((n<Position2D>"game_result_screen/lerp_towards").Scale, dt*10.f )
            monad'{
                let! arena = hermes.arena_view
                let! tournament = hermes.arena_view
                let _, arena = match arena with Left a -> a | Right t -> t.game_with_player ((=)(identity())) tournament_index
                let! game_result = hermes.game_result |> Option.orElse (Some arena.game_result)
                for i, cp_sp in List.indexed <| List.rev (game_result_screen.Value.get_node_children'<Node2D>"character_pillars")do
                    let n_max_pillars = game_result.names.Count
                    let coordinates max_places place i = let max_places, place, i = float32 max_places, float32 place, float32 i in let r_offset = 300.f in Vector2((1920.f - r_offset) - ((1.f - i/(max_places + 1.f)) * (1920.f - 2.f * r_offset)), 350.f + (place/max_places) * 200.f) 
                    let all_players_positions = game_result.places |> Map.toList |> sortBy snd
                    cp_sp.Position <-  (all_players_positions |> Seq.zip ([1..n_max_pillars]) |> List.ofSeq |> List.tryItem i |> Option.map (fun (i, (name, place)) -> coordinates n_max_pillars place i) |> Option.defaultValue (Vector2(-1000.f, -1000.f)))
                    let to_ordinal =
                        let (|EndsWith|_|) (c:string) (s:string) = if s.EndsWith c then Some s else None
                        function | EndsWith "11" s | EndsWith "12" s | EndsWith "13" s -> s ++ "th" | EndsWith "1" s -> s ++ "st" | EndsWith "2" s -> s ++ "nd" | EndsWith "3" s -> s ++ "rd" | s -> s ++ "th"
                    let to_roman = function 1 -> "I" | 2 -> "II" | 3 -> "III" | 4 -> "IV" | 5 -> "V" | 6 -> "VI" | 7 -> "VII" | 8 -> "VIII" | 9 -> "IX" | 10 -> "X" 
                    monad' {
                        let! (name:Identity), place = all_players_positions |> List.tryFind (snd >> (=)(i+1))
                        let pub = arena.gladiators |> find (fun g -> g.identity = name) |> fun g -> g.tezos_pub_hash
                        cp_sp.set_text[
                            "place", to_roman place
                            "money", sprintf "+%d" (compute_match_awards game_result.places game_result.names |> tryPick (function i when pub = i.pub && hermes.in_online_game |> Option.defaultValue false -> Some i.quantity | _ -> None) |> Option.defaultValue 0)
                            "player/name", (fst <| fst name)
                        ]
                        do AuctionHouse.display_char_equipments (cp_sp.n "player") <| snd game_result.names.[name]
                        (cp_sp.n<AnimationPlayer>"player/vpc/vp/body/anim_body").Play(if place = 1 then "dance" else "idle")
                        ()
                    } |> ignore
                ()
            } |> ignore

        
        //-----------LoadingScreen---------
        let _view_loading_screen = 
            this.set_text [
                "loading_screen/VBoxContainer/CenterContainer2/players_waiting", 
                match game_state with | StartMenu((_, _, n, N, _)::_) -> string n + "/" + string N | _ -> "0/N"
            ]
            let names = match game_state with StartMenu((_, names, _, _, _)::_) -> names |> Map.toList | _ -> []
            if names <> [] then do
            let previews1 = (this.get_node_children'<Control> "loading_screen/previews")
            let previews2 = (this.get_node_children'<Control> "loading_screen/previews2")
            for i, p in List.indexed (previews1 ++ previews2) do
                p.Visible <- i < names.Length
                if i < names.Length
                then do
                    let name, arg = names.[i]
                    do AuctionHouse.display_char_equipments (p.n "player") <| snd arg 
                    p.set_text [ "player/name", (fst <| fst name) ]
        
        //-----------Login-Register---------
//        
        //-----------Loading----------------
//        let _loading = this.set_visibility["loading", match shop with Pending -> true | _ -> false]
        //-----------Shop-------------------
        let _view_shop =
//                let! shop = match shop with Task.Ready shop -> Some shop | Pending -> None
//                let pop_up = name_select.GetChild(-1) :?> PopupMenu
            let nshop = this.n<Control> "ah"
            this.toggle_visibilities [ "shop", Input.IsActionJustPressed "toggle_shop" ]
            if nshop.Visible then do
                let name_select = (nshop.n<OptionButton>"character_select")
                let all_names = controller.user_name true ++ controller.user_name false 
                for i, (name, _) in List.indexed all_names do name_select.SetItemText(i, name)
                
                init_and_process
                    this
                    shop_item_scene
                    (match shop with Task.Ready s -> mapi (fun i x -> x.reckless_points * 10000 + ((hash x.name |> abs)%1000 * 10) + ((hash x.owned |> abs)%10), x) s.sale_in_category |> Map.ofSeq | _ -> Map.empty)
                    "shop/scroll_container/grid"
                    (fun (i:ShopItem) (n:Control) ->
                        let nshadow = (n.n<Control>"shadow")
                        nshadow.Modulate <- Colors.White.with_alpha <| if i.owned then 0.f else (if shop.Result.reckless_points < i.reckless_points && shop.Result.hog_points < i.hog_points then 0.55f else 0.20f)
                        n.child_on_events_add [
                            ["prices/price/buy"],"pressed", (fun _ ->
                                async {
                                    do! http.Value.craft {pub = hermes.acc.pub.string; recipe = {requirements = []; ingredients = ["reckless_points", i.reckless_points]; results = Logic.SINGLE(i.name,1)}} 
                                    do! reload_shop hermes.acc.pub.string
                                } |> Async.StartAsTask |> ignore
                                )
                            ["prices/price2/buy"],"pressed", (fun _ ->
                                async {
                                    do! http.Value.craft {pub = hermes.acc.pub.string; recipe = {requirements = []; ingredients = ["hog_points", i.hog_points]; results = Logic.SINGLE(i.name,1)}} 
                                    do! reload_shop hermes.acc.pub.string
                                } |> Async.StartAsTask |> ignore
                                )
                            ["equip"],"pressed",(fun _ ->
                                let name_select = this.n<OptionButton> "shop/character_select"
                                equip hermes.acc.pub.string {pub = hermes.acc.pub.string; item = i.name; quantity = 1} name_select.Selected
                                )
                        ]
                        let [scat; sname] = String.split ["/"] i.name |>> (String.split ["_"] >> map (String.mapi (function 0 -> Char.ToUpper | _ -> id)) >> String.concat " " ) |> List.ofSeq
                        try n.set_texture["img", ResourceLoader.load_texture <| head i.texture_path] with _ -> ()
                        let nimg = n.n<Control> "img"
                        nimg.RectRotation <- match i.category with Weapon -> 51.f | _ -> 0.f
                        n.set_text [
                           "name", sname
                           "prices/price", string i.reckless_points 
                           "prices/price2", string i.hog_points
                        ] 
                        n.set_visibility [
                           "prices/price", not i.owned
                           "prices/price2", not i.owned
                           "equip", i.owned
                        ]
                    ) (konst ignore)
                let sn = this.n "ah"
                let name_select = sn.n<OptionButton> "character_select"
//                let texture cat i = equipments.[name_select.Selected].[cat].item |> Shop.texture_path |> List.item i |> ResourceLoader.Load<Texture>
                AuctionHouse.display_char_equipments (sn.n "preview/player") AuctionHouse.my_equipments.[name_select.Selected]
                match shop with 
                | Task.Ready shop -> 
                    let hog_points = shop.owned |> tryFind (function {item = "hog_points"} -> true | _ -> false) |>> (fun x -> x.quantity) |> Option.defaultValue 0 
                    let reckless_points = shop.owned |> tryFind (function {item = "reckless_points"} -> true | _ -> false) |>> (fun x -> x.quantity) |> Option.defaultValue 0 
                    this.set_text [
                        "shop/money/money", string reckless_points
                        "shop/money/money2", string hog_points
                        "shop/preview/player/name", fst all_names.[name_select.Selected]
                    ]
                    for c in (this.get_node_children'<TextureButton> "shop/categories") do
                        let is_cat = c.Name.ToLower() = shop.category.ToString().ToLower()
                        c.RectScale <- if is_cat then Vector2(1.5f, 1.5f) else (if c.Name = "cash" then Vector2(2.f, 2.f ) else Vector2.One)
                        if c.Name = "cash"
                        then do ()//(c.Material :?> ShaderMaterial).SetShaderParam("line_color", Colors.Crimson)
                        else do (c.Material :?> ShaderMaterial).SetShaderParam("line_color", Colors.SandyBrown.with_alpha (if is_cat then 1.0f else 0.0f ))
                | _ -> ()
        
        //---------Friends----------
        let _view_friends =
            match hermes.game_state, hermes.friends_statuses with
            | StartMenu _, Some all_players ->
                
                
                
                let all_players = all_players |> filter (fst >> snd >> function | Verified pk -> pk <> hermes.acc.pub | _ -> false)
                let friends = Relationships.friendships.friends >>= (fun p -> all_players |> filter (fst >> snd >> (function Verified p' -> p'.string = p | _ -> false)))
                let friends_requests = Relationships.friendships.friend_requests >>= (fun p -> all_players |> filter (fst >> snd >> (function Verified p' -> p'.string = p | _ -> false)))
                let rem_players = List.except friends
                let display_users users cat = 
                    init_and_process this user_box_scene (List.map (fun p -> hash p, p) users |> Map.ofList) (sprintf "start_menu/player_list/scroll/groups/cat_%s/vbox" cat)
                        (fun (((name, signatures),ci),client_state) (pc:Control) ->
                            pc.child_on_events_add [
                                ["hbox/btn_add_friend"], "pressed", (fun _ ->
                                    async {
                                        let! _ = match ci with Verified p -> http.Value.insert_friendship (hermes.acc.pub.string, None, p.string)
                                        do! Relationships.reload_friendships hermes.acc.pub.string
                                        } |> Async.StartAsTask |> ignore
                                    )
                                ["hbox/btn_remove_friend"], "pressed", (fun _ ->
                                    async {
                                        let! _ = match ci with Verified p -> http.Value.insert_friendship (hermes.acc.pub.string, None, p.string) 
                                        do! Relationships.reload_friendships hermes.acc.pub.string
                                        } |> Async.StartAsTask |> ignore
                                    )
                                ["hbox/btn_invade"], "pressed", (fun _ ->
                                    match ci with Verified p -> controller.enqueue_command [Join(IdentityQuery.ByIdentity (name, None, p), pia().[0], Some join_url)] | _ -> ()
                                    )
                                ["hbox/btn_spectate"], "pressed", (fun _ ->
                                    match ci with Verified p -> controller.enqueue_command [Spectate(IdentityQuery.ByName (name, None))] | _ -> ()
                                    )
                            ]
                            )
                        (fun (((name, signatures),ci),client_state) (pc:Control) ->
                            pc.set_text [
                                "name", name
                                "status", match client_state with | ClientState.InGame _ -> "In Game" | Spectating _ -> "Spectating" | LookingForGame _ -> "In Queue" | Disconnected -> "Offline" | _ -> "Online"
                            ]
                            pc.set_visibility [
                                "hbox/btn_spectate", match client_state with | InGame _ -> true | _ -> false
                                "hbox/btn_invade", match client_state with | InGame _ -> true | _ -> false
                            ]
                        )
                display_users all_players "all"    
                display_users friends "friends"    
                display_users friends_requests "requests"    
                ()
            | _ -> ()
        //---------Settings----------
//        print_benchmark "settings view: " (fun _ ->
        
        let _view_settings = 
            let n = this.n<Control>
            let esc_menu = n"settings"
            let controls = n"settings/bg/vbox/controls"
            if esc_menu.Visible then do
                settings <-
                    let parse_input (s:string) : Result<InputKey, string> = tryParse s |> Option.toResultWith ("Bad input: " ++ s)
                    let parse_from_children (r:Node) path = (r.get_node_children' path) |> map (fun (n:Node) -> n.Get("text") |> string |> parse_input) |> List.fold (fun acc x -> match acc, x with | xs, Ok x -> (xs ++ [x]) | acc, _ -> acc) []
                    let parse_from_node (r:Node) path = (r.n path).Get("text") |> string |> parse_input
                    let pdirectional (r:Node) s = match (r.n<OptionButton>s).Selected with | 0 -> Mouse | 1 -> LeftJoystickWASD | 2 -> RightJoystickArrows
                    let poption (r:Node) s k = k (r.n<OptionButton> s).Selected 
                    let pcontrols (r:Node) s =
                        let controls = r.n s
                        let throw = parse_from_children controls "btns_throw"
                        let dash = parse_from_children controls "btns_dash"
                        let aim = pdirectional controls "o_aim"
                        let move = pdirectional controls "o_move"
                        let dashdir = pdirectional controls "o_dashdir"
                        let ai = poption controls "ai" id
                        { throw = throw; dash = dash; aim = aim; move = move; dashdir = dashdir; ai = ai; }
                    let online_controls = pcontrols controls "online" 
                    let local_controls0 = pcontrols controls "local1"
                    let local_controls1 = pcontrols controls "local2"
                    let local_controls2 = pcontrols controls "local3"
                    let local_controls3 = pcontrols controls "local4"
                    let local_controls = [local_controls0; local_controls1; local_controls2; local_controls3]
                    {
                        video = 1
                        audio =
                            let audios = this.get_node_children'<Node>"settings/bg/vbox/audio"
                            Map.ofSeq
                                (List.splitInto (audios.Length/3) audios
                                |> List.map (fun [n;m;v] ->
                                    String.skip 2 n.Name,
                                    (
                                        (m:?>CheckButton).Pressed, float32 (v:?>Slider).Value/ 100.f
                                    ) ))
                        controls = {
                            online_controls = online_controls
                            local_controls = local_controls
                        }
                        local_play_names = let name i = (controls.n<LineEdit>(sprintf "local%d/player" i)).Text in [1..4] |> List.map name |> List.mapi (fun i n -> (n, []), snd settings.local_play_names.[i])
                        use_interpolation = (this.n<CheckBox> ("settings/bg/vbox/other/use_interpolation")).Pressed
                        show_fps = (this.n<CheckBox> ("settings/bg/vbox/other/show_fps")).Pressed
                    }
//            )
        let _view_menu =
            let x = 1920.f/2.f
            let y = 40.562f
            this.set_meta
                "menu"
                "interpolate_towards"
                (function
                    | None -> Vector2(x, y)
                    | Some p when (match hermes.game_state with StartMenu _ -> true | _ -> false) -> Vector2(x, y) 
                    | Some p when Input.IsActionJustPressed "escape" -> if p.y < 0.f then Vector2(x, y) else Vector2(x, -60.f)
                    | Some p -> p
                    )
            let menu = this.n<Control>"menu"
            menu.set_center_position <| menu.get_center_position.LinearInterpolate(this.get_meta<Vector2> "menu" "interpolate_towards" |> Option.defaultValue (Vector2(x, y)), dt * 10.f)
            this.set_visibility [
                "menu/hbox/btn_leave_arena", match hermes.game_state with | StartMenu(_::_) | InArena _ -> true | _ -> false
                "menu/hbox/btn_exit_game", match hermes.game_state with | StartMenu [] -> true | _ -> false
                "menu/hbox/btn_login_register", match hermes.game_state with | StartMenu [] -> true | _ -> false
                "settings", visible_menu |> List.contains "settings"
                "shop", visible_menu |> List.contains "shop"
                "ah", visible_menu |> List.contains "ah"
                "login_register", visible_menu |> List.contains "login_register" && match hermes.game_state with | StartMenu [] -> true | _ -> false
            ]
            this.set_alpha [
                "login_register/vbox/btns/btn_login", if (this.n<Label> "login_register/vbox/label").Text = "Login" then 1.f else 0.7f
                "login_register/vbox/btns/btn_register", if (this.n<Label> "login_register/vbox/label").Text = "Register" then 1.0f else 0.7f
            ]
            this.set_text [
                "start_menu/welcome", "Welcome, " + db.user_name + "!"
                "start_menu/chat/chat",
                    hermes.chat()
                    |> map (function
                            |ServerToClient.Message(((from,_),_), msg) -> sprintf "[%s]: %s" from msg
                            | ServerToClient.Whisper(((from,_),_), to', msg) -> sprintf "[%s] whispers: %s" from msg
                            | ServerToClient.Whisper(from, to', msg) -> sprintf "To [%A]: %s" to' msg
                            )
                    |> String.concat "\n"
            ]
        ()
        
        
        
    override this._Draw() =
        if (Input.IsActionPressed "debug") then do
            let bodies =
                match hermes.arena_model with
                | Some a ->
                    let a = snd <| match a with Left a -> a | Right t -> t.game_with_player (fst >> fst >> (=) db.user_name) tournament_index 
                    [
                        a.axes |> List.map body
                        a.gladiators |> List.map body
                        a.walls
                        a.statues |> List.map body
                        a.limbs |> List.map body
                    ] |> List.concat
                | _ -> []
            for sh in bodies do this.draw_shape sh
            
        
    override this._Notification what =
        if what = MainLoop.NotificationWmQuitRequest
        then
            try kill_js() with _ -> ()
            this.GetTree().Quit()
            