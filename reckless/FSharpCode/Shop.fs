module FSharpCode.Shop
open System
open HOG.Database.Client open hogRequests
open System.Threading.Tasks
open FSharp.Data
open FSharpCode.Exts
open FSharpPlus
open Godot


let http = lazy(HOG.Database.Client.mk_hog_http_client "https://higher-order-games.net:8087/")
type EquipmentSlot = Head | Arms | Legs | Torso | Weapon | Box

let item_category = function | Prefix "arms" _ -> Arms | Prefix "legs" _ -> Legs | Prefix "torso" _ -> Torso | Prefix "head" _ -> Head | Prefix "weapon" _ -> Weapon
type Item with member this.category = item_category this.item
module Shop =
    let texture_path s = 
        match String.split ["/"] s |> List.ofSeq with
        | ["arms"; n] -> ["r_arm/" + n + ".png"; "l_arm/" + n + ".png"]
        | ["legs"; n] -> ["r_leg/" + n + ".png"; "l_leg/" + n + ".png"]
        | [cat; n] -> [cat + "/" + n + ".png"]
        |>> (fun x -> "images/shop/" + x)
    type ShopItem = {name:string; hog_points:int; reckless_points:int; owned: bool} with
        member this.texture_path = texture_path this.name
        member this.category = item_category this.name
    type Shop = {sale: ShopItem list; owned:Item list; category: EquipmentSlot} with
        member this.reckless_points = this.owned |> tryPick (fun i -> if i.item = "reckless_points" then Some i.quantity else None) |> Option.defaultValue 0
        member this.hog_points = this.owned |> tryPick (fun i -> if i.item = "hog_points" then Some i.quantity else None) |> Option.defaultValue 0
        member this.sale_in_category = this.sale |> filter (fun i -> i.category = this.category)
    let empty_shop = {sale = []; owned = []; category = Head}
    let load_shop_and_items (h:HOGHTTPClient) pub cat : Async<Shop> =
        async{
            let! recipes = h.get_all_recipes
            let! owned = h.get_owned_items pub
            let items = recipes |> choose (fun r -> match r.results with SINGLE(x,1) -> Some x | _ -> None)
            let items = items |> Set.ofList |> Set.toList
            let items_prices =
                items
                |>> (fun i -> {
                    name = i;
                    reckless_points = recipes |> filter (function {ingredients = ["reckless_points", q]; results = SINGLE(i',_)} when i = i' -> true | _ -> false) >>= (fun x -> x.ingredients) |> tryHead |>> snd |> Option.defaultValue 0
                    hog_points = recipes |> filter (function {ingredients = ["hog_points", q]; results = SINGLE(i',_)} when i = i' -> true | _ -> false) >>= (fun x -> x.ingredients) |> tryHead |>> snd |> Option.defaultValue 0
                    owned = owned |> exists (fun it -> it.item = i) })
    
            return {sale = items_prices; owned = owned; category = cat |> Option.defaultValue Head}
        }
    let mutable shop = Task.Run (konst empty_shop)
    let reload_shop pub = async {
        let! s = load_shop_and_items http.Value pub None
        shop <- match shop with HOG.Tools.Extras.Task.Ready old_s -> Task.Run(konst {s with category = old_s.category}) | _ -> Task.Run (konst s)
//        printfn "new shop %A" (s.owned)
        return ()
    }
    let change_category cat = shop <- match shop with HOG.Tools.Extras.Task.Ready s -> Task.Run (konst {s with category = cat}) | _ -> shop
module Equipment =
    type Equipments = private Equipments of Map<EquipmentSlot, Item> list ref with
        member this.Item with get(i) = match this with Equipments e -> (!e).[i]

    let starter_equipment pub i :Map<EquipmentSlot,Item>= Map.ofList [
        Head, { item = "head/" + (match i with 0 | 1 -> "green_bandana" | 2 -> "red_bandana" | _ -> "blue_bandana"); pub = pub ; quantity = 1 }
        Arms, { item = "arms/basic"; pub = pub; quantity = 1 }
        Legs, { item = "legs/basic"; pub = pub; quantity = 1}
        Torso, { item = "torso/basic"; pub = pub; quantity = 1 }
        Weapon, { item = "weapon/basic"; pub = pub; quantity = 1 }
    ]
    let private extract (Equipments e) = e
    let private extract_deref (Equipments e) = !e
    let (<---) (Equipments a) b = let _ = a := b in Equipments a
    let mk_starter_equipments pub : Map<EquipmentSlot, Item> list = List.init 5 (starter_equipment pub)
    let equipments = Equipments <| ref (mk_starter_equipments "H2Z+hqtPsLFZI7Fi7hMxg3V++qdZjlxLkrZC5Eh4ROM=")
    let load_equipments pub = equipments <--- File.load (sprintf "user://equipments_%s.save" pub) (mk_starter_equipments "H2Z+hqtPsLFZI7Fi7hMxg3V++qdZjlxLkrZC5Eh4ROM=") //|> tap (extract_deref >> head >> printfn "loaded eqs %A")
    let save_equipments pub = extract_deref equipments |> File.change (sprintf "user://equipments_%s.save" pub) 
    let equip pub (item:Item) i =
        equipments <--- match equipments with Equipments eq -> !eq |> List.mapi (function i' when i = i' && item.pub = pub -> Map.add item.category item | _ -> id)
        save_equipments pub

module Relationships =
    type Friendships = private {pub: string; friendships:Friendship list ref}
        with
        member this.friends = !this.friendships >>= (fun f -> !this.friendships |> List.map (function f' when f'.pub1 = f.pub2 && f'.pub2 = f.pub1 -> if this.pub = f.pub1 then f.pub2 else f.pub1))
        member this.friend_requests = !this.friendships >>= (fun f -> if f.pub2 = this.pub && !this.friendships |> List.contains {pub1 = f.pub2; pub2 = f.pub1} |> not then [f.pub1] else [])
    let friendships : Friendships = {pub = ""; friendships = ref []}
    let reload_friendships pub =
        async{
            let! fs = http.Value.get_pub_friendships pub
            friendships.friendships := fs
        }
    
type DB() =
    member val user_name : string =
        let randomStr (chars:string) = 
            let charsLen = chars.Length
            let random = System.Random()

            fun len -> 
                let randomChars = [|for i in 0..len -> chars.[random.Next(charsLen)]|]
                new System.String(randomChars)
        "Guest" ++ randomStr "0123456789" 5 with get, set