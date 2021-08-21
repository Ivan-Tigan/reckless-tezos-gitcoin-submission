module FSharpCode.Model

open System
open System.IO
open System.Runtime.Serialization.Formatters.Binary
open System.Text
open System.Threading.Tasks
open EnginePrime
open EnginePrime.EnginePrime
open EnginePrime.EnginePrime.Networking
open FSharp.Control.Tasks.TaskBuilder
open FSharpCode.Exts

open FSharpCode
open FSharpCode.Shop
open FSharpPlus
open FSharpPlus.Internals
open FSharpx.Collections
open FSharp.Collections
open Godot
open FSharp.Data
open FSharpCode.Exts
open HOG.Database.Client
open Newtonsoft.Json
open HOG.Tools open HOG.Tools.Physics open HOG.Tools.Anim open Cooldown open Timer open Random
open EnginePrime.GameLogic

type InputDirectionType = MousePosition | ControllerDirection

type ClientInput = {
    ai:int
    move_dir: Vec
    aim_dir: Vec
    dash_dir: Vec
    is_charge_pressed: bool
    is_run_pressed: bool
    emote: byte
}
    with
    static member charge_pressed i = i.is_charge_pressed 
    static member run_pressed i = i.is_run_pressed
    static member basic = { ai = 0; move_dir = Vec.zero; aim_dir = Vec.Zero; dash_dir = Vec.Zero; is_charge_pressed = false; is_run_pressed = false; emote = 0uy }

//module ClientInput =
//    let direction (gpos :Vec) ({move_dir = v}:ClientInput) = v//match t with | MousePosition -> (if gpos.distance_to v > fix64 10 then gpos.direction_to' v else Vec.zero) | ControllerDirection -> v 

[<Struct>]  
type LimbType = LH of axe:Networking.Identity option | RH of Networking.Identity option | LL | RL | H
    with static member basic_shape = Circle(fix64 50)
type Skin = SkinBody of LimbType | SkinTorso | SkinAxe



            

module Body =
    let basic x y shape = {
         position = Vec(fix64 x, fix64 y)
         velocity = Vec.zero 
         area = shape
    }
[<Struct>] 
type Limb = {
    limb_type : LimbType * Networking.Identity
    body : Body
    initial_velocity : Vec
    flight_duration : Timer
} with member this.can_be_picked_up = this.flight_duration |> Timer.completed//this.body.velocity.length = fix64 0
module Limb =
    let inline can_be_picked_up (l:Limb) = l.can_be_picked_up
[<Struct>]
type Axe = {
    owner : Networking.Identity
    body: Body
    just_thrown: bool
    was_thrown : bool
    initial_velocity: Vec
    latest_direction: Vec
    flight_duration: Timer
}
    with
    member inline this.can_be_picked_up = this.body.velocity.length <= fix64 100
    member inline this.harmful = this.was_thrown && not this.can_be_picked_up
    static member base_speed = fix64 750
    static member max_speed = fix64 1450
    static member base_shape = Circle (fix64 50)
module Axe =
    let inline harmful (a:Axe) = a.harmful
    let inline can_be_picked_up (a:Axe) = a.can_be_picked_up
//module PhysX =
//    type XBody<'owner> = private B of 'owner inref * Body ref
//    let inner_body (B b) = !b
//    let mk_xbody = ref >> B
//    type PhysXEngine = private P of XBody list ref with
//        member this.mk_body b = match this with P bs -> let rb = mk_xbody b in let _ = bs := rb::(!bs) in rb
//        member this.move f jo jeo o b =
//            match this, b with P bs, B b' ->
//            let bs = !bs |> List.map inner_body
//            let ob = !b' in
//            let nb = f bs ob
//            b' := nb 
//            b, List.collect (fun other -> match Body.overlapping ob other |> not, Body.overlapping nb other with false, true -> jo other | true, false -> jeo other | true, true -> o other | _ -> []) bs 
//let noev _ = []
//let world (pe:PhysX.PhysXEngine) =
//    let ps = ["bob", pe.mk_body (Body.basic 10 10 (Circle(fix64 3))) ]
//    let nps = ps |> List.map (mapItem2 (pe.move (Body.move_and_slide fix64.One) (fun o -> if o) noev noev))
//    ps
    
type Gladiator = {
    identity: Networking.Identity
    tezos_pub_hash: string
    equipment: AuctionHouse.Equipment
    ai_difficulty: int
    invader: Timer option
    competence: int * Timer
    charge_direction : Vec
    body: Body
    limbs: (LimbType * Networking.Identity) list
    charge: Timer
    throw_speed_bonus: fix64
    speech : int
    charge_press_timer: Timer
    fail_catch_penalty_timer: Timer
    just_caught_axe_in_air: bool
    dash: (Vec * Timer) Cooldown
}
    with
    member this.ai = this.ai_difficulty > 0
    member g.name = fst <| fst g.identity 
    member g.bloodlust gs = fix64 1 + clamp (fix64 0) (fix64 1) (fract (fst g.competence)  30 + (fix64 g.limbs.Length - fract (gs |> List.filter (fun g -> g.limbs.Length > 0) |> List.sumBy (fun g -> g.limbs.Length)) gs.Length)/(fix64 7)) 
    member inline this.n_free_hands = if (not this.has_axe_l && this.has_lh) && (not this.has_axe_r && this.has_rh) then 2 else if (not this.has_axe_l && this.has_lh) || (not this.has_axe_r && this.has_rh) then 1 else 0
    member this.has_lh = this.limbs |> List.exists (fst >> function | LH _ -> true | _ -> false)
    member this.has_rh = this.limbs |> List.exists (fst >> function | RH _ -> true | _ -> false)
    member this.has_limb l = match l with | LH _ -> this.has_lh | RH _ -> this.has_rh | l -> this.limbs |> List.exists (fst >> (=)l)
    member this.has_axe_l = this.limbs |> List.exists (fst >> function | LH(Some _) -> true | _ -> false)
    member this.has_axe_r = this.limbs |> List.exists (fst >> function | RH(Some _) -> true | _ -> false)
//    static member direction_to_input g = Option.map ClientInput.mouse_position >> Option.defaultValue Vec.zero >> (fun x -> x - g.body.position) >> Vec.normalized
    static member base_speed = fix64 300
    member this.is_charging = this.charge |> Timer.running
    member this.just_threw inp = (this.has_axe_l || this.has_axe_r) && not inp.is_charge_pressed && Timer.running this.charge && inp.aim_dir.length > fix64.Zero |> tap (function true -> printfn "just thrown" | _ -> ())

    static member remove_axe ls =
        let rec remove' completed ls =
            match completed, ls with
            | true, x -> x
            | false, [] -> []
            | false, (LH (Some _), o)::ls -> (LH None, o)::ls
            | false, (RH (Some _), o)::ls -> (RH None, o)::ls
            | false, l::ls -> l::(remove' false ls)
        remove' false ls
    member g.limbs_picked_up ls =
        let missing_hands, missing_legs = g.limbs |> List.fold (fun (h, l) lt -> match lt with LH _, _ | RH _, _ -> (h-1, l) | LL, _ | RL, _ -> (h, l-1) | _ -> h, l) (2,2)
        ls
        |> List.indexed
        |> List.filter (snd >> Limb.can_be_picked_up)
        |> List.filter (snd >> body >> Body.overlapping g.body)
        |> List.choose (
                           fun (i, x) ->
                               match x.limb_type with
                               | RH a , n | LH a, n -> (if not g.has_lh || not g.has_rh then Some(i, {x with limb_type = RH a, n}) else None)
                               | LL,n | RL, n -> (if not (g.has_limb LL) || not (g.has_limb RL) then Some (i, {x with limb_type = RL, n}) else None)
                               | lt, n -> if not (g.has_limb lt) then Some (i, x) else None
                       )
        |> List.fold (fun ((mh, ml, ls) as acc) (i, lt) -> match lt.limb_type with LH _, _ | RH _, _ when mh > 0 -> (mh-1, ml, (i, lt)::ls) | RL, _ | LL, _ when ml > 0 -> (mh, ml-1, (i, lt)::ls) | H, _ -> (mh,ml,(i,lt)::ls) | _ -> acc) (missing_hands, missing_legs, [])
        |> thrd3//possible to get two limbs at the same time
    member g.limb_types_picked_up ls = g.limbs_picked_up ls |> List.map (fun (i, x) -> match x.limb_type with | (LH (Some _), o) -> LH None, o | (RH (Some _), o) -> RH None, o | x -> x)
    member g.axes_picked_up dt axes = axes |>  List.indexed |> List.choose (fun (i, x) ->
        let can_catch = g.fail_catch_penalty_timer |> Timer.running |> not || (g.has_axe_l <> g.has_axe_r)
        let grab = g.charge_press_timer |> Timer.running_not_completed 
        if (Axe.can_be_picked_up x || grab) && (x |> body |> Body.just_overlapping dt g.body) then Some (i, x, grab) else None) |> fun l -> List.take (min l.Length g.n_free_hands) l
    member g.num_limbs_lost dt old_axes = if g.limbs.IsEmpty then 0 else match g.dash with Active _ -> 0 | _ -> (old_axes |> List.filter Axe.harmful |> List.except (g.axes_picked_up dt old_axes|> List.map snd3) |> List.filter (fun axe -> if axe.just_thrown && axe.owner = g.identity then Body.overlapping g.body axe.body else Body.just_overlapping dt g.body axe.body) |> List.length |> clamp 0 g.limbs.Length)
module Gladiator =
    let incompetence_timer = Timer.mk_timer 0 3
    let basic x y identity tez_pubh eq = {
        ai_difficulty = 0
        tezos_pub_hash = tez_pubh
        equipment = eq
        body = Body.basic x y (Rect(fix64 40, fix64 80))
        limbs = [LH (Some identity), identity; LL, identity; RH (Some identity), identity; RL, identity; H, identity]
        charge = Timer(fix64 0, fract 5 2)
        identity = identity
        speech = 0
        charge_direction = Vec.zero
        charge_press_timer = Timer(fix64 0, fract 1 2)
        fail_catch_penalty_timer = Timer(fix64 0, fract 2 2)
        throw_speed_bonus = fix64.Zero
        just_caught_axe_in_air = false
        dash = Ready
        invader = None
        competence = 0, incompetence_timer }
    let inline remove_axe ls = Gladiator.remove_axe ls
    let inline limbs_picked_up ls (g:Gladiator) = g.limbs_picked_up ls
    let inline limb_types_picked_up ls (g:Gladiator) = g.limb_types_picked_up ls
    let inline axes_picked_up dt axes (g:Gladiator) = g.axes_picked_up dt axes
    let num_limbs_lost dt old_axes (g:Gladiator) = g.num_limbs_lost dt old_axes

[<Struct>]
type StatueType = BerserkerStatue | LegendStatue
[<Struct>]
type Statue = {statue_type : StatueType; body: Body}
[<Struct>]
type BloodSpot = {body: Body; duration: Timer}
module Ease =
    let in_expo t = if t = fix64.Zero then fix64.Zero else  fix64.pow (fix64 2) (((fix64 10) * t) - fix64 10);
type MatchResult = {timestamp:DateTime; names: string list; places: (string * int) list}
[<Struct>]
type SideEffects = {
    limbs_picked_up: int 
    axes_picked_up: int 
    axes_thrown: int
    limbs_cut: int
    axe_impacts: int
    axes_caught: int
    dashes: int
}

type Arena = {
    id_gladiators: Map<int, Gladiator> 
    elimbs: Map<int, Limb>
    eaxes: Map<int, Axe>
    statues: Statue list
    walls: Body list
    rand: int
    blood_spots: Map<int, BloodSpot> 
    game_result: GameResult<string * AuctionHouse.Equipment>
    side_effects: SideEffects
}
    with
    member arena.gladiators = arena.id_gladiators |> Map.valueList
    member arena.limbs = Map.valueList arena.elimbs //|> List.sortBy (fun l -> l.body.position.x)
    member arena.axes = Map.valueList arena.eaxes //|> List.sortBy (fun l -> l.body.position.x)
    member inline arena.axe_collision_layer = arena.walls ++ (List.map body arena.statues) ++ (List.choose (fun (g:Gladiator) -> match g.invader with Some(t) when not (Timer.completed t) -> Some {g.body with area = Circle (fix64 100)} | _ -> None) arena.gladiators)
//    member old_arena.limbs_picked_up dt = old_arena.gladiators >>= Gladiator.limbs_picked_up old_arena.limbs
    member old_arena.limbs_axes_picked_up dt = old_arena.gladiators |> List.map (fun (g:Gladiator) -> g, (g.limbs_picked_up old_arena.limbs, g.axes_picked_up dt old_arena.axes)) |> Map.ofList
        
    member old_arena.limbs_cut (random:MRandom) dt =
        old_arena.gladiators
        >>= (fun g ->
                let num_limbs_lost = Gladiator.num_limbs_lost dt old_arena.axes g
                g.limbs |> List.take num_limbs_lost |> List.indexed |> List.map (fun (i, x) ->
                                                                            let vel =
                                                                                        let direction = Vec(random.rangef (fix64 -1) (fix64 1), random.rangef (fix64 -1) (fix64 1)) + g.body.position.direction_to (Vec.vec 950 525) |> Vec.normalized
                                                                                        let speed = fix64 <| random.rangei 1200 1800
                                                                                        direction * speed
                                                                            random.nexti, {
                                                                            limb_type = match x with | LimbType.LH a, n | RH a, n -> RH a, n | LL, n | RL, n -> RL, n | x -> x
                                                                            body = {
                                                                                g.body with
                                                                                    velocity = vel                                                                                        
                                                                                    area = LimbType.basic_shape
                                                                            }
                                                                            flight_duration = Timer(fix64 0, random.rangef (fract 4 10) (fract 8 10))
                                                                            initial_velocity = vel})
            ) |> Map.ofList
    member old_arena.axes_thrown (random:MRandom) dt inps =
        old_arena.gladiators
            |> List.indexed
            >>= fun (index, g) ->
                        let i = inps |> Map.find g.identity
                        if g.just_threw i && i.aim_dir.length > fix64.Zero
                        then
                                let initial_velocity = 
                                    let direction = i.aim_dir.normalized
                                    let speed = clamp fix64.Zero (fix64 5500) (g.throw_speed_bonus + (Axe.base_speed) + Axe.max_speed * Timer.completion g.charge (* g.bloodlust old_arena.gladiators - fix64 1*))
                                    direction * speed
                                [
                                random.nexti, {
                                  body = Body.place dt (old_arena.axe_collision_layer)
                                                                                           {position = g.body.position + (i.aim_dir |> Vec.scaled (fix64 1))
                                                                                            velocity = initial_velocity
                                                                                            area = Axe.base_shape}
                                  was_thrown = true
                                  initial_velocity = initial_velocity
                                  flight_duration = Timer(fix64.Zero, (fract 9 2) + g.charge.completion)
                                  owner = g.identity
                                  latest_direction = initial_velocity.normalized
                                  just_thrown = true}
                             ] else []
            |> Map.ofList
    member old_arena.new_limbs_axes (random:MRandom) (dt:fix64) inps : Map<int, Limb> * Map<int, Axe> =
        let throwns = old_arena.axes_thrown random dt inps 
        let new_limbs = old_arena.limbs_cut random dt
        let flyings = 
               new_limbs |> Map.toList |> List.indexed
               >>= (fun (i, (_, l)) ->
                    match l.limb_type with
                    | LH (Some _), o | RH (Some _), o ->
                    let initial_velocity =
                        let direction = Vec(random.rangef (fix64 -1) (fix64 1), random.rangef (fix64 -1) (fix64 1)) |> Vec.normalized
                        let speed = fix64 <| random.rangei 1200 1800
                        direction * speed
                    [random.nexti, {
                        body = { l.body with velocity = initial_velocity }
                        was_thrown = false
                        initial_velocity = initial_velocity
                        flight_duration = Timer(fix64 0, random.rangef (fract 2 10) (fract 8 10))
                        owner = snd l.limb_type
                        latest_direction = initial_velocity.normalized
                        just_thrown = false
                        }]
                    | _ -> [])
               |> Map.ofList
        let new_axes = Map.union throwns flyings |> Map.mapValues (fun a -> {a with body = a.body|> Body.place_inside (Vec.one * (fix64 80)) (Vec(fix64 1840, fix64 1000)) })
        new_limbs, new_axes
    member arena.ai difficulty (g:Gladiator) =
        let aim_g, aim_dir = arena.gladiators |> filter ((<>)g) |> List.maxBy (fun g' -> (if g'.limbs.IsEmpty then -1000 else (match difficulty with 1 -> id | _ -> (fun x -> -x)) (length g'.limbs))) |> fun g' -> g', g.body.position.direction_to g'.body.position
        let find_axe = if not (g.has_lh || g.has_rh) || arena.axes.IsEmpty then Vec.zero else (arena.axes |> List.minBy (fun a -> if not a.harmful then (a.body.position - g.body.position).length else fix64 100000) |> fun a -> g.body.position.direction_to a.body.position) 
        let run_away = arena.axes |> List.map (fun a -> if a.harmful then a.body.direction.rotated (fix64 90) else Vec.zero) |> sum |> fun v -> (v * fix64 5)
        let get_limbs = (try (arena.limbs |> List.filter (fun l -> l.limb_type |> fst |> g.has_limb |> not) |> List.minBy (fun l -> g.body.position.distance_to l.body.position) |> fun l -> g.body.position.direction_to l.body.position) with | _ -> Vec.zero)
        let direction =
            let dir =
                if g.has_axe_l || g.has_axe_r
                then aim_dir
                else find_axe + match difficulty with 1 -> get_limbs | _ -> (get_limbs * fix64 5) + run_away
            dir.normalized'
        
        {

            ai = 1
            is_charge_pressed = (g.has_axe_l || g.has_axe_r) && match difficulty with 1 -> not <| Timer.completed g.charge | _ -> Timer.completion g.charge < if aim_g.limbs.Length < 3 then fract 1 4 else fract 2 4
            is_run_pressed = match difficulty with 1 -> false | _ -> run_away.length > get_limbs.length
            emote = 0uy
            move_dir = direction 
            aim_dir = direction
            dash_dir = direction
        }





let reckless = {
    start =
        fun (args:unit) timestamp online players ->
        {
           id_gladiators = [
               Gladiator.basic 200 200 
               Gladiator.basic 1650 850 
               Gladiator.basic 200 850 
               Gladiator.basic 1650 200 
           ] |> Seq.map2 (fun (n,(pubh,eq)) g -> g n pubh eq) (Map.toSeq players) |>> (fun g -> hash g.identity, g) |> Map.ofSeq
           elimbs = Map.empty
           eaxes = Map.empty
           statues = [
    //            {statue_type = BerserkerStatue; body = Body.basic 300 500 (Rect (fix64 35, fix64 100))} ;
    //            {statue_type = LegendStatue; body = Body.basic 1600 500 (Rect (fix64 35, fix64 100))} 
           ]
           walls =
               [
                   Body.basic -50 -50 (Rect(fix64 50, fix64 2000))
                   Body.basic -50 -50 (Rect(fix64 2000, fix64 50))
                   Body.basic 1950 1100 (Rect(fix64 50, fix64 2000))
                   Body.basic 1950 1100 (Rect(fix64 2000, fix64 50))
               ]
           rand = 0
           game_result = { places = Map.empty; names = players; timestamp = timestamp }
           blood_spots = Map.empty
           side_effects = { limbs_picked_up = 0

                            axes_picked_up = 0
                            axes_thrown = 0

                            limbs_cut = 0
                            axe_impacts = 0
                            axes_caught = 0
                            dashes = 0 } 
        }                              
    step = fun rseed dt inps old_arena ->
        
        let random = mk_mrandom rseed
        let inps = Map.ofList (List.map (fun g -> g.identity, inps |> Map.tryFind g.identity |> Option.defaultValue ClientInput.basic |> fun i -> if i.ai > 0 then old_arena.ai i.ai g else i) old_arena.gladiators)
        
        let g_limbs_picked_up_axes_picked_up = old_arena.limbs_axes_picked_up dt
        let new_limbs, new_axes = old_arena.new_limbs_axes random dt inps
        let limbs_picked_up = g_limbs_picked_up_axes_picked_up |> Map.valueList |> List.map fst |> List.concat
        let axes_picked_up = g_limbs_picked_up_axes_picked_up |> Map.valueList |> List.map snd |> List.concat
        
        {
            old_arena with
            id_gladiators =
                old_arena.id_gladiators
                |> id_bind  (fun g ->
                    let inp = inps |> Map.find g.identity
//                    let inp = if inp.ai > 0  then old_arena.ai inp.ai g else inp
                    let num_limbs_lost = Gladiator.num_limbs_lost dt old_arena.axes g
                    let limb_types_picked_up =
                        Gladiator.limb_types_picked_up old_arena.limbs g
                        |> List.fold (fun acc l -> match l with LH a, n | RH a, n -> (if g.has_lh || List.exists (function LH _, _ -> true | _ -> false) acc then RH a, n else LH a, n)::acc | RL, n | LL, n -> (if g.has_limb LL || List.exists (function LL, _ -> true | _ -> false) acc then RL, n else LL, n)::acc | h -> h::acc) []
                    let axes_picked_up = snd g_limbs_picked_up_axes_picked_up.[g]
                    let num_axes_picked_up = length axes_picked_up 
                    
                    let rec grab_axes name (n:int) (ls)  =
                        match n, ls with
                        | 0, ls -> ls
                        | _, [] -> []
                        | n, (LH None, o)::ls -> (LH (Some name), o):: (grab_axes name (n-1) ls)
                        | n, (RH None, o)::ls -> (RH (Some name), o):: (grab_axes name (n-1) ls)
                        | n, l::ls -> l :: (grab_axes name n ls)
                        
                    if g.limbs.IsEmpty then Some g else
                        match g.invader with
                        | Some(t) when not (Timer.completed t) -> Some {g with invader = g.invader |> Option.map (Timer.tick dt)}
                        | _ -> 
                    Some {   
                        g with
                    ai_difficulty = inp.ai
                    limbs =
                        limb_types_picked_up ++
                        (g.limbs
                         |> List.drop num_limbs_lost
                         |> if g.just_threw inp then Gladiator.remove_axe else id)
                        |> grab_axes g.identity num_axes_picked_up
                    charge = g.charge |> Timer.tick_or_restart (dt * g.bloodlust old_arena.gladiators) ((g.has_axe_l || g.has_axe_r) && inp.is_charge_pressed) 
                    charge_press_timer = g.charge_press_timer |> Timer.tick_or_restart dt (inp.is_charge_pressed && (Timer.running_not_completed g.charge_press_timer || Timer.running_not_completed g.fail_catch_penalty_timer |> not))
                    fail_catch_penalty_timer =
//                        if g.name = "Player 1" then do
//                            let escape = let b = [|0x1buy|] in b |> Encoding.ASCII.GetString
//                            GD.Print(escape + "[2J" + escape)
//                            GD.Print("timers: \n", g.charge_press_timer, "; \n", g.fail_catch_penalty_timer, "\n \n")
                        g.fail_catch_penalty_timer |> Timer.tick_or_restart dt (not (g.has_axe_l || g.has_axe_r) && (g.charge_press_timer |> Timer.running || Timer.running_not_completed g.fail_catch_penalty_timer))
                    body = {
                        (g.body |> Body.place_inside Vec.zero (Vec(fix64 1920, fix64 1080))|> Body.move_and_slide_around dt ((old_arena.statues |> map body) ++ old_arena.walls) |> Body.place dt ((old_arena.statues |> map body) ++ old_arena.walls)) 
                            with
                            velocity =
                                let speed = if Timer.running g.charge then fix64 0 else g.bloodlust old_arena.gladiators * Gladiator.base_speed * fract (pown 2 (g.limbs |> filter (function | LL, o | RL, o -> true | _ -> false) |> length)) 3 
                                let direction = inp.move_dir
                                match g.dash with Active(v, t) -> v | _ -> direction * speed
                    }
                    speech = int inp.emote
                    charge_direction = inp.aim_dir
                    throw_speed_bonus =
                        let bonus_speed = axes_picked_up |> map (function (_, a, true) -> a.body.speed | _ -> fix64.Zero) |> fold (+) fix64.Zero
                        if inp.is_charge_pressed then (if bonus_speed <> fix64.Zero then bonus_speed else g.throw_speed_bonus) else fix64.Zero
                    just_caught_axe_in_air = axes_picked_up |> List.exists thrd3
                    dash =
                        match g.dash with
                        | Ready when inp.is_run_pressed && not inp.is_charge_pressed-> Active(inp.dash_dir * fix64 1500 * g.bloodlust old_arena.gladiators * fract (g.limbs |> filter (fun l -> match fst l with | LL | RL -> true | _ -> false) |> length  |> (+) 2) 3 , Timer(fix64.Zero, fract 1 7))
                        | Active(v, t) when Timer.completed t -> Cooldown (Timer(fix64.Zero, fract 4 2))
                        | Active(v, t) -> Active(v, Timer.tick dt t)
                        | cd when g.just_caught_axe_in_air -> Ready
                        | cd -> Cooldown.tick dt cd
                    competence =
                        match g.competence with
                        | c, t when Timer.completed t -> c-1, Gladiator.incompetence_timer
                        | c, t when g.just_caught_axe_in_air -> clamp 0 10 (c+3), Gladiator.incompetence_timer
                        | c, t when g.just_threw inp -> clamp 0 10 (c+1), Gladiator.incompetence_timer
                        | c, t when g.is_charging -> c, t
                        | c, t -> c, Timer.tick dt t
                    }
                    )
//                    ) 
            elimbs =
                (Map.union new_limbs old_arena.elimbs)
//                    |> List.except (Arena.limbs_picked_up dt old_arena |> List.map snd)
                |> id_bind (fun limb ->
                    let exist = if List.exists (fun l -> l = limb) (limbs_picked_up |> List.map snd) then konst None else Some
                    exist {
                    limb with
                        flight_duration = limb.flight_duration |> Timer.tick dt
                        body = {
                                (limb.body |> Body.place_inside Vec.zero (Vec(fix64 1920, fix64 1080))|> Body.move_and_collide dt old_arena.axe_collision_layer)
                                with
                                    velocity = 
                                        let speed = fix64.lerp  (limb.flight_duration |> Timer.completion' Ease.in_expo) limb.initial_velocity.length fix64.Zero
                                        limb.body.direction * speed
                        } 
                    }) 
            eaxes = 
                (Map.union new_axes old_arena.eaxes)
//                    |> List.except (Arena.axes_picked_up dt old_arena |> List.map snd3) 
                |> id_bind (fun axe ->
                    let exist = if List.exists (fun a -> a = axe) (axes_picked_up |> List.map snd3) then konst None else Some
                    exist {
                    axe with
                        flight_duration = Timer.tick dt axe.flight_duration
                        latest_direction = if axe.body.velocity.length = fix64.Zero then axe.latest_direction else axe.body.velocity.normalized
                        just_thrown = false
                        body = 
                            let b = (axe.body |> Body.place_inside Vec.zero (Vec(fix64 1920, fix64 1080))|> Body.move_and_bounce dt old_arena.axe_collision_layer)
                            {b 
                                with
                                velocity =
                                    let dir = b.direction
                                    let speed = fix64.lerp (Timer.completion axe.flight_duration) axe.initial_velocity.length fix64.Zero
                                    dir*speed
                        } 
                               
                })

            game_result =
                if not old_arena.game_result.winner.IsEmpty
                then old_arena.game_result
                else
                    
                    let winner = (old_arena.gladiators >>= (fun g -> if g.limbs.IsEmpty then [] else [g.identity])) |> (fun gs -> if gs.Length = 1 then [gs.Head, 1] else []) |> Map.ofList
                    {
                        old_arena.game_result
                        with
                            places =
                                let old_death_order = old_arena.game_result.places in
                                old_arena.gladiators 
                                >>= (fun g -> if g.limbs.Length = 0 && old_death_order |> Map.exists (fun n p -> n = g.identity) |> not then [g.identity, old_arena.gladiators.Length - old_death_order.Count] else [])
                                |> Map.ofList
                                |> fun n -> n ++ winner ++ old_death_order
                    }
            blood_spots =
                Map.union
                    (old_arena.blood_spots |> Map.mapValues (fun bs -> { bs with duration = if old_arena.limbs |> List.exists (fun l -> l.body.position = bs.body.position) then Timer.tick dt bs.duration else bs.duration} ))
                    ((old_arena.limbs
                        |> List.collect (fun {body = b} ->
                            if b.velocity.length = fix64.Zero && old_arena.blood_spots |> Map.forall (fun _ bs -> bs.body.position <> b.position)
                            then [random.nexti, { body = {Body.basic 0 0 (Circle fix64.Zero) with position = b.position}; duration = Timer.mk_timer 0 10} ]
                            else []))
                        |> Map.ofList
                    )
                
            side_effects =
                {
                    limbs_picked_up = limbs_picked_up.Length 
                    axes_picked_up = axes_picked_up.Length - (old_arena.gladiators |> List.filter (fun g -> g.just_caught_axe_in_air) |> length)
                    axes_caught = old_arena.gladiators |> List.filter (fun g -> g.just_caught_axe_in_air) |> length
                    dashes = old_arena.gladiators |> List.filter (fun g -> match g.dash with Cooldown.Active(_, Timer(t, _)) when t = fix64.Zero -> true | _ -> false) |> length
                    axes_thrown = new_axes |> length
                    limbs_cut = new_limbs |> length
                    axe_impacts = old_arena.axes |> List.filter (fun a -> old_arena.axe_collision_layer |> List.exists (fun c -> Body.just_overlapping dt a.body c)) |> List.length  |> tap (fun x -> if x <> 0 then printfn "axecolmodel: %A" x else ())
                }
    }
    side_effects = fun a -> a.side_effects
    game_result = fun old_arena online names ->
        let d_ord = old_arena.game_result.places |> Map.keys
        let names_are_dead = List.forall (fun n -> d_ord |> Seq.contains n) names
        let winner = names |>  List.exists (fun name -> old_arena.game_result.winner |> Set.contains name)
        if  names_are_dead || winner
        then Some(old_arena.game_result)
        else None
    names = fun a -> a.gladiators  |> List.map (fun g -> g.identity)
    lerp = fun dt interpolated_arena arena ->
         let delta_fixed = dt * fix64 15
         let body_lerp = Body.lerp delta_fixed
         let timer_lerp = Timer.lerp delta_fixed
         let vec_lerp = Vec.lerp delta_fixed
         {
         arena with
             id_gladiators =
                 Lerp.lmap (fun g f -> {f with charge_direction = vec_lerp g.charge_direction f.charge_direction; body = body_lerp g.body f.body; charge = timer_lerp g.charge f.charge; dash = Cooldown.lerp delta_fixed g.dash f.dash}) interpolated_arena.id_gladiators arena.id_gladiators
             elimbs =  Lerp.lmap (fun (l:Limb) f -> { f with body = body_lerp l.body f.body; flight_duration = timer_lerp l.flight_duration f.flight_duration } : Limb)  interpolated_arena.elimbs arena.elimbs 
             eaxes =  Lerp.lmap (fun (a:Axe) f -> { f with body = body_lerp a.body f.body; flight_duration = timer_lerp a.flight_duration f.flight_duration } : Axe) interpolated_arena.eaxes arena.eaxes
             blood_spots = arena.blood_spots//List.lerp (fun (a:BloodSpot) f -> { f with duration = timer_lerp a.duration f.duration } : BloodSpot) [] interpolated_arena.blood_spots arena.blood_spots
         }
    predict_inputs = fun a -> id
    join = fun arena n -> if arena.gladiators |> List.filter (fun (g:Gladiator) -> g.limbs.Length > 0) |> length = 1 || arena.gladiators |> List.exists (fun g -> g.invader.IsSome || g.identity = fst n) then None else Some {arena with id_gladiators = Map.add (hash <| fst n) {Gladiator.basic (1920/2) (1080/2) (fst n) (fst <| snd n) (snd <| snd n) with invader = Some(Timer(fix64.Zero, fix64 4))} arena.id_gladiators} |> tap (printfn "someone wants to join and this is what I send: %A")
    on_input_event = fun _ (_:Map<Networking.Identity,unit>) -> id
    }
let reckless_normal_or_tournament = normal_or_tournament (countdown_start reckless)
let compute_match_awards places (peeps:Map<Identity, string * AuctionHouse.Equipment>) = places |> Map.toList >>= (function ((_, (CryptographicIdentity.Verified _) as pub), place) -> [{pub = fst peeps.[pub]; item = "0"; quantity = (match place with 1 -> 80 | 2 -> 20 | 3 -> 10 | _ -> 5)}] | _ -> [])  
