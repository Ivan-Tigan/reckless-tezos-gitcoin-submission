module FSharpCode.Players

open EnginePrime.EnginePrime.Networking

let inline generic_comparer a b = if a > b then 1 elif a < b then -1 else 0
type PlayersOrdering = ByName | ByGameState | ByBounty
type Players = {
    players: (string * ClientState) list
    ordering: PlayersOrdering
} //with
//    member this.ordered =
//       let c = (fun (n1:string, gs1:string, b1:int) (n2, gs2, b2) -> match this.ordering with ByName -> generic_comparer n1 n2 | ByGameState -> generic_comparer gs1 gs2 | ByBounty -> generic_comparer b1 b2)
//       this.players |> List.sortWith c