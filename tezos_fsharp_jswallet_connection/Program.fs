module HOG.Tezos.Client
open System
open System.Collections.Generic
open System.Net.Http
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System.Text
open FSharpPlus

type HOGHTTPClient = private HOGHTTPClient of System.Net.Http.HttpClient * string * string
let mk_hog_http_client url proxy = HOGHTTPClient (new System.Net.Http.HttpClient(), url, proxy)

type TezosAccount = {priv: string; pub: string; pub_hash: string}

type TokenMetadata = {name: string; symbol: string; decimals: int; shouldPreferSymbol: bool; thumbnailUri: string; textures: string list}

type TokenProposal = {initial_supply: (string * int) list; token_id: int; token_info:TokenMetadata}

type AuctionOffer = {end_: DateTime; price: (string * int * int); product: (int * int * int) list list; seller: string;start_: DateTime}

module Serializer =
    let getUTF8 (str: byte []) = System.Text.Encoding.UTF8.GetString(str)
    let jsonToObject<'t> json =
        JsonConvert.DeserializeObject(json, typeof<'t>) :?> 't

    let JSON v =
        let jsonSerializerSettings = new JsonSerializerSettings()
        jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()
        JsonConvert.SerializeObject(v, jsonSerializerSettings)
open Serializer

module GenericRequests =
    let getAsync<'b> (client:HttpClient) (url:string) =
        async {
  
            let! response = client.GetAsync(url) |> Async.AwaitTask
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let content: ('b) = jsonToObject<'b> content
            return content
        }
    
    let postAsync<'a,'b> (client:HttpClient) (url:string) (body: 'a) =
        async {
            let json = JsonConvert.SerializeObject body
            printfn "The json is: %s" json
            use content = new StringContent(json, Encoding.UTF8, "application/json")
            let! response = client.PostAsync(url, content) |> Async.AwaitTask
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let content: ('b) = jsonToObject<'b> content
            return content
        }
open System.Text.RegularExpressions

open System.Text.RegularExpressions

open GenericRequests

type t = Initial of (int * int)
let tezos_tuple<'a> xs =
    let m = new Dictionary<string, 'a> ()
    for i, x in List.indexed xs do m.Add(string i, x)
    m

[<AutoOpen>]
module hogRequests =
    type HOGHTTPClient with
        member this.http_client = match this with HOGHTTPClient(c,_,_) -> c
        member this.url = match this with HOGHTTPClient(_,url,_) -> url
        member this.proxy = match this with HOGHTTPClient(_,_,proxy) -> proxy
        member this.http_proxy str = match String.startsWith "https" str with
                                        | true -> let _ = printfn $"%s{this.proxy}%s{str}" in $"%s{this.proxy}%s{str}" 
                                        |_ -> let _ = printfn $"%s{str}" in str
        member this.post_request<'a,'b> url data = postAsync<'a,'b> this.http_client (this.http_proxy (this.url + url)) data
        member this.get_request<'b, 'r> url (s:'b -> 'r) (f:exn ->'r) = try getAsync<'b> this.http_client (this.http_proxy url) |>> s with | e -> async { return f e}
       
        member this.login (priv: string) = this.post_request<{|priv: string|}, TezosAccount> "login" {|priv = priv|}
        
        member this.call (contract_address:string) (function_name:string) (arguments:'a) = this.post_request<(string * string * 'a), Result<string,string>> "contract" (contract_address, function_name, arguments)
        
        member this.post_ah_offer contract_address pubh (start_:DateTime) (end_:DateTime) (token_id:int, quantity:int) product =
            this.call contract_address "post" [{| start_ = start_.ToUniversalTime(); end_ = end_.ToUniversalTime(); price = tezos_tuple ["";pubh; string token_id; string quantity]; seller = pubh; product = product |> List.map (List.map (fun (a,b,c) -> tezos_tuple[a;b;c]))|}]
        
        member this.propose_tokens contract_address (ps:(int * (string * int) list * string) list) : Async<Result<string,string>> =
            this.call
                contract_address
                "propose_tokens"
                <| List.map
                    (fun (tid:int,initial_supply,tm:string) ->
                    let hex = K4os.Text.BaseX.Base16.ToHex (Encoding.UTF8.GetBytes tm) 
                    {| initial_supply = initial_supply |> List.map (fun (a:string,b:int) -> tezos_tuple[a;string b]); token_id = tid; token_info = hex |})
                    ps
        
        
        member this.get_metadata (map_id: string) = this.post_request<{|map_id: string|}, (int * TokenMetadata) list> "get_metadata" {|map_id = map_id|} |>> Map.ofList
        
        member this.get_token_proposals  (map_id:string) = this.post_request<{|map_id: string|}, (string * (TokenProposal list)) list> "get_token_proposals" {|map_id = map_id|} |>> Map.ofList  
        
        member this.get_balance (pub_hash:string) =
            this.get_request<{|spendable_balance: float|}, float> $"https://api.florence.tzstats.com/explorer/account/%s{pub_hash}"
                (fun res -> res.spendable_balance)
                (konst -1.)
        
        member this.get_auction_house_offers (map_id:string) =
            this.get_request<{|key: string; value: {|end_: DateTime; price: Dictionary<int, string>; product: Dictionary<int, int> list list; seller: string;start_: DateTime|}|} list, Map<string, AuctionOffer>> $"https://api.florence.tzstats.com/explorer/bigmap/%s{map_id}/values"
                (List.map (fun res ->
                    (res.key, {
                        end_ = (res.value.end_).ToUniversalTime()
                        price = (res.value.price.[0], int res.value.price.[1], int res.value.price.[2])
                        product = res.value.product |> List.map (List.map (fun dict -> (dict.[0], dict.[1], dict.[2]))) 
                        seller = res.value.seller
                        start_ = (res.value.start_).ToUniversalTime()
                        }
                ))>> Map.ofList)
                (konst Map.empty)
        
        member this.get_ledger (map_id: string) =
            this.get_request<{|key:Dictionary<int, string>; value:int|} list, Map<(string* int), int>> $"https://api.florence.tzstats.com/explorer/bigmap/%s{map_id}/values"
                (List.map (fun res -> ((res.key.[0], int res.key.[1]), res.value)) >> Map.ofList)
                (konst Map.empty)
        
        member this.get_fa2_quantity (map_id:string) (address:string) (token_id:int) =
            this.get_request<{|key:Dictionary<int, string>; value:int|}, int> $"https://api.florence.tzstats.com/explorer/bigmap/%s{map_id}/%s{address},%d{token_id}"
                (fun res -> res.value)
                (konst 0)
                
        member this.get_fa2_quantities (map_id:string) (l:(string * int) list) =
            l |>> (fun (a,b) -> this.get_fa2_quantity map_id a b) |> List.toSeq |> Async.Sequential |>> List.ofArray

[<EntryPoint>]
let main argv =
    0