module FSharpCode.AuctionHouse

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.Net
open System.Net.Configuration
open System.Net.Security
open System.Text
open FSharpCode.Shop
open FSharpPlus.Control
open Godot
open HOG.Tezos.Client
open Exts
open FSharpPlus

(*
displayUrl
filePath
imgUrl

*)
(*
offer metadata
*)

//return maps instead of lists,
//? return ints instead of strings for product

let async_loading loading a = async {let _ = loading true in let! res = a in let _ = loading false in return res} 
let download_imgs (token_metadata:Map<int,TokenMetadata>) =
    let hashpng = hash_string >> (fun x -> x + ".png")
    let wc = new WebClient()
    let dir = OS.GetUserDataDir()
    let (+/) p1 p2 = System.IO.Path.Combine(p1,p2)
    printfn "download beginsf %A" token_metadata
    Map.toList token_metadata >>= (fun (tid, m) -> if tid < 1000 then [] else let _ = printfn "try download %A" m in try m.textures >>= fun t -> if File.exists (dir +/ hashpng t) then [] else [wc.AsyncDownloadFile(Uri (http_proxy t), dir +/ hashpng t)] with _ -> []) |> Async.Sequential |>> ignore

let (|Range|_|) min max i = if min <= i && i < max then Some i else None 
let category = function
    | Range 1000 2000 i -> Shop.Weapon
    | Range 2000 3000 i -> Shop.Head
    | Range 3000 4000 i -> Shop.Torso
    | Range 4000 5000 i -> Shop.Arms
    | Range 5000 6000 i -> Shop.Legs
    | _ -> Shop.Box
type Equipment = private Eq of Map<EquipmentSlot, int> ref
let my_equipments = List.init 5 (konst (Map.ofList <| List.init 5 (fun i -> let tid = 1000 * (i+1) in category tid, tid)) >> ref >> Eq)
let equipment (Eq eq) slot = !eq |> Map.find slot
let private equip i tid = match my_equipments.[i] with Eq eq -> eq := !eq |> Map.add (category tid) tid |> tap (konst <| printfn "equipped something: %A" !eq)
    
let texture_path (m:Map<int, TokenMetadata>) i =
    let user_path = m |> Map.tryFind i |>> (fun v -> v.textures |>> hash_string |>> (fun x -> "user://" + x + ".png")) |> Option.defaultValue []
    let res_path = 
        let name = m |> Map.tryFind i |>> (fun v -> v.name) |> Option.defaultValue "Error" |> String.replace " Head" "" |> String.replace " Torso" "" |> String.replace " Arms" "" |> String.replace " Legs" "" |> String.toLower |> String.replace " " "_"
        i
        |> category
        |> string
        |> String.toLower
        |> (fun x -> match x with
            | "arms" ->  [sprintf "r_arm/%s.png"; sprintf "l_arm/%s.png"] |>> ((|>) name)
            | "legs" ->  [sprintf "r_leg/%s.png"; sprintf "l_leg/%s.png"] |>> ((|>) name)
            | _ -> [sprintf "%s/%s.png" x name]
            )
        |>> (sprintf "res://images/shop/%s")
    if user_path.IsEmpty then res_path else user_path
printfn "path %A" AppDomain.CurrentDomain.BaseDirectory
let tezos = mk_hog_http_client "http://localhost:34199/" (http_proxy "")
let mutable private acc = lazy (tezos.login "edskRqp5FXa4YmZFh6tvKLpYR6WmwqvoSEreQCafxeeJfwdshUknrv3bxSaiQPcib9fiGAAfKuZPzyETZr5Uy7nxVUMjUbinnM" |> Async.RunSynchronously)
let contract_address = "KT1Mw7E46UuQk62imBoYzTSUCpuz3LLXZ7qo"
let offers_address = "122422"
let token_metadata_address = "122426"
let ledger_address = "122423"
let token_proposals_address = "122427"
let bid (offer_id:int) (quantity:int) = tezos.call contract_address "bid" [tezos_tuple[offer_id; quantity]]
let ah_item_scene = ResourceLoader.Load'<PackedScene> "res://scenes/ah/ah_item.tscn"
let ah_item_view_scene = ResourceLoader.Load'<PackedScene> "res://scenes/ah/ah_item_view.tscn"
let ah_box_preview_scene = ResourceLoader.Load'<PackedScene> "res://scenes/ah/ah_preview_box.tscn"
let display_item_view (metadata:Map<int, TokenMetadata>) sell propose (n:Node) token_id quantity chance k_preview =  
    n.set_visibility ["sell", sell; "propose", propose]
    n.set_text [
        "name", metadata.[token_id].name
        "quantity", if quantity > 1 then string quantity + "x" else ""
        "chance", if chance = 100 then "" else string chance + "%"
    ]
    let text =  (texture_path metadata (int token_id)).[0]
    n.set_texture [ "img", ResourceLoader.load_texture text ]
    (n.n<Control> "img").RectRotation <- match category (int token_id) with Shop.Weapon -> 45.f | _ -> 0.f
    if List.contains token_id [-1;-2;-3] then do n.child_on_events_add [ ["btn_preview"], "pressed", k_preview]
let create_box_preview (ahn:Node) = spawn ahn ah_box_preview_scene.Value
let destroy_box_preview (ahn:Node) = 
    try (ahn.n "ah_preview_box").Free() |> konst(printfn "destroyed old") with _ -> printfn "fail destroy old"
let refresh_box_preview ahn = let _ = destroy_box_preview ahn in create_box_preview ahn
let display_box_preview (ahn:Node) k_box_prev k_close =
    let box_preview = refresh_box_preview ahn
    k_box_prev box_preview
    box_preview.child_on_events_add [ ["btn_close"], "pressed", (fun _ -> let _ = box_preview.QueueFree() in k_close())]
    box_preview
let display_box_preview_ah (ahn:Node) display_item_view xs k_close =
    display_box_preview ahn (fun box_preview ->
    for token_id, quantity, chance, k_preview in xs do
        let item_view = spawn (box_preview.n "scroll/grid") ah_item_view_scene.Value in
        display_item_view false false item_view token_id quantity chance (fun _ -> let _ = box_preview.QueueFree() in k_preview())
    ) k_close
let clean_item_grid (ahn:Node) = for c in ahn.get_node_children'<Node> "scroll_container/grid" do c.QueueFree()
let kraw = fun (a,b,c) -> a,b,c,ignore
let display_ah_items loading (ahn:Node) tm items_to_display =
    clean_item_grid ahn
    let display_item_view = display_item_view tm
    let display_box_preview = display_box_preview_ah ahn display_item_view
    for oid, off in items_to_display do
        let n = spawn (ahn.n "scroll_container/grid") ah_item_scene.Value
        n.set_text ["prices/price/price", off.price |> item3 |> string]
        n.set_visibility["prices",true;"inv", false; "remaining", true]
        (n.n<Button> "prices/bid/btn_bid").Disabled <- off.seller = acc.Value.pub_hash || off.end_ < DateTime.Now
        n.child_on_events_add [
            ["prices/bid/btn_bid"], "pressed", (fun _ -> bid (int oid) (n.get_spinbox_value "prices/bid/amount" |> int) |> async_loading loading |>> ignore |> Async.StartImmediate)
            ["remaining"], "process", (fun _ -> n.set_text["remaining", let rem = off.end_.ToUniversalTime().Subtract(DateTime.Now.ToUniversalTime()) in if rem.TotalSeconds >= 0. then sprintf "%02d:%02d:%02d" rem.Hours rem.Minutes rem.Seconds else "Ended"])
        ]
        let n = n.n "ah_item_view"
        match off.product with
        | [[(token_id, quantity, 100)]] -> display_item_view false false n token_id quantity 100 ignore
        | xs when forall (function [_,_,100] -> true | _ -> false) xs ->
            let xs = xs |> List.concat |>> kraw
            do display_item_view false false n -1 1 100 (fun _ -> display_box_preview xs ignore |> ignore)
        | [xs] ->
            display_item_view false false n -2 1 100 (fun _ -> display_box_preview (xs |>> kraw) ignore |> ignore)
        | xs ->
            let collection, mystery = xs |> List.partition (function [_] -> true | _ -> false) |> mapItem1 (List.concat >> map kraw)
            display_item_view false false n -3 1 100 (fun _ -> let rec disp() = display_box_preview ((-1, 1,100, fun _ -> display_box_preview collection (fun _ -> let _ = printfn "closer" in disp() |> ignore) |> ignore)::(mystery |>> fun xs -> (-2,1,100, fun _ -> display_box_preview (xs |>> kraw) (disp >> ignore) |> ignore))) ignore in disp() |> ignore)
let display_inv_items loading (ahn:Node) tm k_offer_posted items_to_display =
    clean_item_grid ahn
    let display_item_view = display_item_view tm
    let box = display_box_preview ahn (fun box_preview -> box_preview.set_visibility["sell", true] ) ignore
    box.set_visibility ["sell", true]
    let grid = box.n "scroll/grid"
    for tid, q in items_to_display do
        let n = spawn (ahn.n "scroll_container/grid") ah_item_scene.Value
        n.set_visibility["prices",false;"inv", true; "remaining", false]
        display_item_view false false (n.n "ah_item_view") tid q 100 (fun _ -> printfn "TODO: equip")
        printfn "initialize inv %A" tid
        n.child_on_events_add[
            ["inv/equip"], "pressed", (fun _ ->
                do equip (ahn.get_option_button_selected "character_select") tid
                printfn "Equipped: %A" my_equipments
                )
            ["inv/sell"], "pressed", (fun _ ->
                printfn "pressed sell %A" tid
                let iv = spawn grid ah_item_view_scene.Value in
                iv.set_meta "sell" "tid" (konst tid)
                printfn "meta set: %A" (iv.get_meta<int> "sell" "tid")
                let _ = iv.child_on_events_add [["sell/btn_close"], "pressed", iv.QueueFree] in
                display_item_view true false iv tid q 100 ignore )
        ]
        let posting = box.n<Node> "sell"
        posting.child_on_events_add [
            ["post"], "pressed", (fun _ ->
                let price = posting.get_spinbox_value "price"
                let duration = posting.get_spinbox_value "duration"
                let product =
                    box.get_node_children'<Node> "scroll/grid"
                    |> Seq.fold (fun acc (iv:Node) ->
                        let boxid = iv.get_spinbox_value "sell/boxid"
                        let p = iv.get_meta<int> "sell" "tid" |> tap (printfn "meta get %A") |> Option.defaultValue (let _ = printfn "meta tid not set" in -1), iv.get_spinbox_value "sell/quantity" |> int, iv.get_spinbox_value "sell/chance" |> int
                        Map.change boxid (function Some xs -> Some(p::xs) | _ -> Some [p]) acc
                        ) Map.empty
                    |> Map.values |> List.ofSeq |>> List.sortBy item3
                tezos.post_ah_offer contract_address acc.Value.pub_hash (DateTime.UtcNow.AddMinutes 1.) (DateTime.UtcNow.AddMinutes (1. + duration)) (0, int price) product |>> ignore >>= konst k_offer_posted |> async_loading loading |> Async.StartImmediate
                )
        ]
        ()
let display_token_proposals loading (ahn:Node) token_metadata proposal_token_metadata k_proposed (props:Map<string,TokenProposal list>) =
    printfn "in proposals"
    clean_item_grid ahn
    let display_item_view = display_item_view proposal_token_metadata
    let box_preview = display_box_preview ahn (fun box_preview -> box_preview.set_visibility["propose", true] ) ignore
    for prop in props |> Map.values |> List.ofSeq |> List.concat do
        printfn "displaying proposal %A" prop
        try 
        let iv = spawn (ahn.n"scroll_container/grid") ah_item_view_scene.Value
        iv.set_text ["name", prop.token_info.name; "quantity", prop.initial_supply |> List.sumBy snd |> string]
        iv.set_visibility ["chance", false]
        iv.set_texture ["img", texture_path proposal_token_metadata prop.token_id |> head |> ResourceLoader.load_texture]
        (iv.n<Control> "img").RectRotation <- match category (int prop.token_id) with Shop.Weapon -> 45.f | _ -> 0.f
        with e -> eprintfn "failed displaying proposal %A %A" prop e
    box_preview.child_on_events_add [
        ["propose/btn_propose"], "pressed", (fun _ ->
            let ns = box_preview.get_node_children'<Node> "scroll/grid"
            ns |> Seq.map (fun n ->
                let name = n.get_text "propose/name" 
                let quantity = n.get_spinbox_value "propose/quantity" |> int
                let catid = (n.get_option_button_selected "propose/category" + 1) * 1000 + 1
                let tid = token_metadata |> Map.keys |> Seq.filter (fun x -> x >= catid && x < catid + 999) |> fun x -> try Seq.min x + 1 with | _ -> catid
                let images = n.get_meta<string seq> "propose/file_dialog" "last_selected_files" |>> List.ofSeq |> Option.defaultValue []
                let symbol = "rex" + (category tid |> string |> take 1 |> String.toLower) + (String.split [" "] name |>> (String.toUpper >> take 3) |> String.concat " ")
                skynet_upload_token_metadata name symbol 0 false (head images) images |>> (fun x -> tid, [acc.Value.pub_hash, quantity], x)
                ) |> Async.Parallel >>= (fun x -> x |> List.ofArray |> tezos.propose_tokens contract_address |>> ignore >>= konst k_proposed) |> async_loading loading |> Async.StartImmediate
            )
        ["propose/btn_add"], "pressed", (fun _ ->
            let iv = spawn (box_preview.n "scroll/grid") ah_item_view_scene.Value
            iv.set_visibility ["propose", true]
            display_item_view false true iv -1 1 100 (fun _ -> (iv.n<FileDialog> "propose/file_dialog").Popup_())
            iv.child_on_events_add[
                ["propose/file_dialog"], "selected", (fun _ ->
                    let imgs = iv.get_meta<string seq> "propose/file_dialog" "last_selected_files" |>> List.ofSeq |> Option.defaultValue []
                    iv.set_texture ["img", ResourceLoader.load_texture (head imgs)]
                    )
                ["propose/btn_close"], "pressed", (fun _ -> iv.QueueFree())
            ]
            ()
            )
    ]
    
let login priv = async {
    let! _acc = tezos.login priv in
    acc <- lazy _acc
}
let get_account k = k acc.Value
let offline_token_metadata = (Map.ofList [
     1000, { name = "Basic"; symbol = "rexBASIC"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = ["https://trello.com/1/cards/5f7b060f364b05303e3c016b/attachments/6118df18cac39754bcddc152/previews/6118df18cac39754bcddc159/download"]}
     2000, { name = "Green Bandana"; symbol = "rexhGRNBNA"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = ["https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118df8bd51fb54a9375d2ca/previews/6118df8cd51fb54a9375d2d3/download"]}
     3000, { name = "Basic Torso"; symbol = "rextBASIC"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = ["https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118dfde1ac6144b9a036448/previews/6118dfdf1ac6144b9a03644c/download"] }
     4000, { name = "Basic Arms"; symbol = "rexaBASIC"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = ["https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118dfb16cabd41f9e61acd6/previews/6118dfb16cabd41f9e61acdb/download"; "https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118dfa188a2df4ad55c8cfb/previews/6118dfa288a2df4ad55c8d01/download"] }
     5000, { name = "Basic Legs"; symbol = "rexlBASIC"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = ["https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118dfc14fad533804bb475c/previews/6118dfc14fad533804bb4760/download"; "https://trello.com/1/cards/6118df7f72e29e8eb86d2f64/attachments/6118dfea4e3a8d592caee345/previews/6118dfea4e3a8d592caee350/download"] }
     -1, { name = "Collection"; symbol = "COL"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = [] }
     -2, { name = "Mystery"; symbol = "MYS"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = [] }
     -3, { name = "Chest"; symbol = "CHE"; decimals = 0; shouldPreferSymbol = false; thumbnailUri = "none"; textures = [] }
 ])
let fetch_token_metadata = 
    tezos.get_metadata token_metadata_address
    |>> Map.union offline_token_metadata
let mutable private token_metadata = Map.empty//fetch_token_metadata |> Async.RunSynchronously
let cached_token_metadata k = k token_metadata
let download_token_imgs = async{let! tm = fetch_token_metadata in let! proptm = tezos.get_token_proposals token_proposals_address |>> (fun ps -> ps |> Map.values |> List.ofSeq |> List.concat |> List.map (fun p -> p.token_id, p.token_info) |> Map.ofSeq) in let! _ = download_imgs tm in return! download_imgs proptm} 
let offers (categories: EquipmentSlot list) (metadata:Map<int, TokenMetadata>) search = tezos.get_auction_house_offers offers_address |>> (Map.filter (fun k v -> let itemid tid = (match v.product with [[x]] -> tid | _ -> -1) in (v.product |> List.exists (List.exists (fun (tid,_,_) -> categories.IsEmpty || List.contains (category (itemid tid)) categories))) && v.product |> List.exists (List.exists (fun (tid,_,_) -> metadata.[int tid].name.Contains search )) ))
let inventory pubh = tezos.get_ledger ledger_address |>> Map.choosei(fun (k,t) v -> if k = acc.Value.pub_hash then Some (t,v) else None) |>> Map.values
let display_char_equipments (n:Node) eq =
    let texture cat i = equipment eq cat |> texture_path token_metadata |> List.item i |> ResourceLoader.load_texture
    n.set_texture [
        "vpc/vp/body", texture Torso 0
        "vpc/vp/body/head", texture EquipmentSlot.Head 0
        "vpc/vp/body/l_hand", texture Arms 1
        "vpc/vp/body/l_hand/axe", texture Weapon 0
        "vpc/vp/body/r_hand", texture Arms 0
        "vpc/vp/body/r_hand/axe", texture Weapon 0 
        "vpc/vp/body/l_leg", texture Legs 1
        "vpc/vp/body/r_leg", texture Legs 0
    ] 

let rec display (n:Node2D) (path:string) =
    async {
        let loading b = let _ = printfn "loading %A" b in let _ = n.set_visibility["loading", b] in n.set_text["loading/label", "Processing Transaction"]
        printfn "After 10 min %A" (DateTime.Now.AddMinutes 10.)
        let! (metadata : Map<int, TokenMetadata>) = fetch_token_metadata |>> Map.union offline_token_metadata//Async.StartChild(fetch_token_metadata, 300) |> Async.join
        token_metadata <- metadata
//        printfn "2 %A" metadata
        let ahn = n.n path
        let categories = ahn.get_node_children'<TextureButton> "categories" |> List.choose (fun (n:TextureButton) -> let name = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase n.Name in if n.Pressed then union_from_string<EquipmentSlot> name else None)
        let search = ahn.get_text "search"
        printfn "categories %A" categories
        let! reckless_points = tezos.get_fa2_quantity ledger_address acc.Value.pub_hash 0
        let! tezos_points = tezos.get_balance acc.Value.pub_hash
        ahn.set_text [
            "money/money", System.String.Format("{0:#,0}", reckless_points)
            "money/money2", System.String.Format("{0:#,0}", tezos_points)
        ]
        let section ah inv prop = if (ahn.n<Button> "section/ah").Pressed then ah() else if (ahn.n<Button> "section/inventory").Pressed then inv() else if (ahn.n<Button> "section/proposals").Pressed then prop() else ah()
        printfn "5"
        do! section
                (fun _ ->
                    printfn "at ah"
                    offers categories metadata search |>> Map.toSeq |>> (fun items -> display_ah_items loading ahn metadata items) )
                (fun _ ->
                    printfn "at inv"
                    inventory acc.Value.pub_hash
                    |>> (Seq.choose(fun (tid, q) -> if tid >= 1000 && (categories.IsEmpty || List.contains (category tid) categories) then Some <| (tid, q) else None))
                    |>> (display_inv_items loading ahn metadata (display n path))
                    )
                (fun _ -> download_token_imgs >>= (fun _ -> tezos.get_token_proposals token_proposals_address) |>> (fun props -> display_token_proposals loading ahn metadata (props |> Map.toList |> List.collect snd |> List.map (fun v -> v.token_id, v.token_info) |> Map.ofList |> Map.union offline_token_metadata) (display n path) props))

        return ()
        
    }