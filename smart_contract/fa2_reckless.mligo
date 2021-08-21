#if ! FA2_INTERFACE
#define FA2_INTERFACE

type token_id = nat
type transfer_destination = [@layout:comb] { to_ : address; token_id : token_id; amount : nat; }
type transfer = [@layout:comb] { from_ : address; txs : transfer_destination list; }
type balance_of_request = [@layout:comb] { owner : address; token_id : token_id; }
type balance_of_response = [@layout:comb] { request : balance_of_request; balance : nat; }
type balance_of_param = [@layout:comb] { requests : balance_of_request list; callback : (balance_of_response list) contract; }
type operator_param = [@layout:comb] { owner : address; operator : address; token_id: token_id; }
type update_operator = [@layout:comb] | Add_operator of operator_param | Remove_operator of operator_param
type token_metadata = [@layout:comb] { token_id : token_id; token_info : (string, bytes) map; }

type token_metadata_storage = (token_id, token_metadata) big_map
type token_metadata_param = [@layout:comb] { token_ids : token_id list; handler : (token_metadata list) -> unit; }
type contract_metadata = (string, bytes) big_map

(* FA2 hooks interface *)

type transfer_destination_descriptor = [@layout:comb] { to_ : address option; token_id : token_id; amount : nat; }

type transfer_descriptor = [@layout:comb] { from_ : address option; txs : transfer_destination_descriptor list }

type transfer_descriptor_param = [@layout:comb] { batch : transfer_descriptor list; operator : address; }

(*
Entrypoints for sender/receiver hooks

type fa2_token_receiver =
  ...
  | Tokens_received of transfer_descriptor_param

type fa2_token_sender =
  ...
  | Tokens_sent of transfer_descriptor_param
*)

#endif
type product = (token_id * nat * nat) list list 
let seller_items (p:product) = List.fold_left (fun ((acc, ps):(token_id * nat) list * (token_id * nat * nat) list) -> List.fold_left (fun ((acc, (a,b,_)): (token_id * nat) list * (token_id * nat * nat)) -> (a,b)::acc) acc ps ) ([]:(token_id * nat) list) p
type price = (address * token_id * nat)
type offer = {
  seller: address; 
  price: price; 
  product: product;
  start_: timestamp;
  end_: timestamp;
}
type auction_house = {
  counter:nat;
  price_ids: token_id set;
  offers: (nat, offer) big_map ; 
}
type ledger = ((address * nat), nat) big_map
type minters = (address, token_id set) big_map
type propose_new_token_params = {token_id: token_id; token_info:bytes; initial_supply:(address * nat) list}
type approve_reject_new_token_params = {proposer: address; decision: bool} 
type token_proposals = (address, propose_new_token_params list) big_map 
type storage = {
  minters: minters;
  registrar: address;
  ledger: ledger;
  ah: auction_house; 
  token_metadata: token_metadata_storage;
  token_proposals: token_proposals;
  metadata: contract_metadata;
}
let can_transfer (ts:transfer list) = List.fold_left (fun ((b, t): bool * transfer) -> b && (Tezos.source = t.from_)) true ts
let transfer ((s, ts):storage * transfer list) = 
  let new_ledger = 
    List.fold_left (fun ((l, t):(ledger * transfer)) -> 
      List.fold_left (fun ((l, r):ledger * transfer_destination) -> 
        match (Big_map.find_opt (t.from_, r.token_id) l, Big_map.find_opt (r.to_, r.token_id) l : (nat option * nat option)) with
        | Some n1, Some n2 -> 
          let _ = if r.amount > n1 then failwith "FA_INSUFFICIENT_BALANCE" else () in 
          let remove = Big_map.update (t.from_, r.token_id) (Some (abs(n1 - r.amount))) l in
          let add = Big_map.update (r.to_, r.token_id) (Some (r.amount + n2)) remove in
          add
        | Some n1, None -> 
          let _ = if r.amount > n1 then failwith "FA2_INSUFFICIENT_BALANCE" else () in 
          let remove = Big_map.update (t.from_, r.token_id) (Some (abs(n1 - r.amount))) l in
          let add = Big_map.update (r.to_, r.token_id) (Some (r.amount)) remove in
          add
        | None, _ -> (failwith "FA2_INSUFFICIENT_BALANCE" : ledger)
      ) l t.txs) s.ledger ts in
  ([]:operation list), {s with ledger = new_ledger}
let mint ((s, tds) : storage * transfer_destination list) =
  let new_ledger = 
    List.fold_left (fun ((acc, td):ledger * transfer_destination) ->
      let _ = match (Big_map.find_opt Tezos.sender s.minters : token_id set option) with | Some ts -> (if Set.mem td.token_id ts then () else failwith "No permission to mint this token") | _ -> failwith "No minting permission" in
      let _ = if Big_map.mem td.token_id s.token_metadata then () else failwith "This token is not registered so it cannot be minted." in
      Big_map.update 
        (td.to_, td.token_id) 
        (Some (
            match (Big_map.find_opt (td.to_, td.token_id) acc : nat option) with 
            | Some n ->  n + td.amount 
            | _ -> td.amount)
        ) acc) 
        s.ledger
        tds  in
  ([]:operation list), {s with ledger = new_ledger}

let approve_reject_new_tokens ((s, rs):storage * approve_reject_new_token_params) = 
  let _ = if Tezos.sender = s.registrar then () else failwith "You have no premission to approve/reject proposals." in
  let proposal = match Big_map.find_opt rs.proposer s.token_proposals with Some ts -> ts | _ -> (failwith "Proposal does not exist.": propose_new_token_params list) in  
  let new_proposals = Big_map.remove rs.proposer s.token_proposals in
  let new_metadatas_ledger = 
    List.fold_left (fun (((acc2, acc3), r):(token_metadata_storage * ledger) * propose_new_token_params) -> 
      (if rs.decision && Big_map.mem r.token_id acc2 then (failwith "Token already registered":token_metadata_storage) else (if rs.decision then Big_map.add r.token_id ({token_id=r.token_id; token_info=Map.literal[("",r.token_info)]}) acc2 else acc2)),
      (if rs.decision then List.fold_left (fun ((l,(a, n)):ledger * (address * nat)) -> Big_map.add (a, r.token_id) n l) acc3 r.initial_supply else acc3)
    ) (s.token_metadata, s.ledger) proposal in 
  ([]:operation list), {s with token_proposals = new_proposals; token_metadata = new_metadatas_ledger.0; ledger = new_metadatas_ledger.1}
let propose_new_tokens ((s,rs):storage * propose_new_token_params list) = 
  let _ = match Big_map.find_opt Tezos.sender s.token_proposals with Some _ -> failwith "You can make 1 proposal at a time." | _ -> () in 
  ([]:operation list), {s with token_proposals = Big_map.add Tezos.sender rs s.token_proposals}
let post ((s, post_offers):storage * offer list) = 
  let _ = List.iter (fun (o:offer) -> if o.start_ < Tezos.now || o.end_ < o.start_ then failwith "New offers must start after now and end after they have started." else ()) post_offers in
  let _ = List.iter (fun (o:offer) -> 
    match o.price with 
    | p -> if not (Set.mem p.1 s.ah.price_ids) then failwith "This is not a registered fungible currency ." else ()
    //| _ -> failwith "Posting an offer must start with an initial price."
    ) post_offers in
  let _ = List.iter (fun (o:offer) -> List.iter (fun (ps : (nat*nat*nat) list) -> if 100n = List.fold_left (fun ((acc, p) : nat * (nat*nat*nat)) -> acc+p.2) 0n ps then () else failwith "Probabilities of items in each box must add up to 100.") o.product) post_offers in
  let _ = List.iter (fun (o:offer) -> List.iter (fun (ps: (nat*nat*nat) list) -> let _ = List.fold_left (fun (((accb,accn), p): (bool*nat) * (nat*nat*nat)) -> if p.2 >= accn then true, p.2 else (failwith "Probabilities of items in each box must be in ascending order.":bool*nat) ) (true,0n) ps in ()) o.product) post_offers in
  let new_counter_offers = List.fold_left (fun (((i,os),o):(nat * (nat, offer) big_map) * offer) -> i+1n, Big_map.add i o os) (s.ah.counter, s.ah.offers) post_offers in 
  let new_counter = new_counter_offers.0 in 
  let new_offers = new_counter_offers.1 in
  let txs_to_lock = 
    List.fold_left (fun ((acc, o):transfer_destination list * offer) -> 
      List.fold_left (fun ((acc, (token_id, n)): transfer_destination list * (token_id * nat )) -> {to_ = Tezos.self_address; token_id = token_id; amount = n}::acc) acc (seller_items o.product)
    ) ([]:transfer_destination list) post_offers in
  let s =  {s with ah = {s.ah with offers = new_offers; counter = new_counter}} in
  let s = transfer (s, [{from_ = Tezos.source; txs = txs_to_lock}]) in
  s
let bid ((s, (offer_id_amts)) : storage * (nat * nat) list) = 
  let new_offers_transfer =
    List.fold_left (fun (((acc1,acc2), (offer_id, amt)) : ((nat, offer) big_map * transfer list) * (nat * nat)) ->
    match (Big_map.find_opt offer_id s.ah.offers : offer option) with
    | Some o ->
      let _ = if Tezos.source = o.seller then failwith "You cannot bid on your own items." else () in
      let _ = if o.end_ < Tezos.now then failwith "Bidding period for this auction has passed." else () in
      if amt > o.price.2 
      then 
        (Big_map.add offer_id ({o with price = (Tezos.source, o.price.1, amt)}) acc1), 
        {from_ = Tezos.source; txs = [{to_ = Tezos.self_address; token_id = o.price.1; amount = amt}]}::(if o.price.0 <> o.seller then {from_ = Tezos.self_address; txs = [{to_ = o.price.0; token_id = o.price.1; amount = o.price.2}]}::acc2 else acc2)
      else (failwith "You must bid higher than the current highest bid." : (nat, offer) big_map * transfer list)
    | _ -> (failwith "Invalid auction house offer id." : (nat, offer) big_map * transfer list)
    ) (s.ah.offers, ([]:transfer list)) offer_id_amts  in
  let s = {s with ah.offers = new_offers_transfer.0} in 
  let s = transfer (s, new_offers_transfer.1) in
  s
let next_nat (n:nat) = (22695477n*n+1n) mod (Bitwise.shift_left 2n 32n)
let finalize ((s, (seed, offer_ids)) : storage * (nat * nat list)) = 
  let new_offers_txs = 
    List.fold_left (fun (((r, acc1,acc2), offer_id) : (nat * (nat, offer) big_map * transfer list) * nat) -> 
      match (Big_map.find_opt offer_id acc1 : offer option) with 
      | Some o -> 
        if Tezos.now >= o.end_ 
        then
          next_nat(r), 
          (Big_map.update offer_id (None:offer option) acc1: (nat, offer) big_map),
          {
            from_ = Tezos.self_address; 
            txs = 
              List.fold_left (fun ((acc, p):transfer_destination list * (nat*nat*nat) list) -> 
              List.fold_left (fun ((acc, p): transfer_destination list * (nat*nat*nat)) -> 
              if r mod 100n > p.2 || List.length acc > 0n
              then acc
              else
              {to_ = o.price.0; 
              token_id = p.0; 
              amount = p.1}::acc) acc p ) 
              ([]:transfer_destination list) o.product
          }::
          (if o.price.0 <> o.seller 
          then [{from_ = Tezos.self_address; txs = [{to_ = o.seller; token_id = o.price.1; amount = o.price.2}]}]
          else [])
        else (failwith "Offer cannot be finalized yet." : nat * (nat, offer) big_map * transfer list)
      | _ -> (failwith "Offer does not exist" : nat * (nat, offer) big_map * transfer list)
    ) (seed, s.ah.offers, ([]:transfer list)) offer_ids in
  let s = {s with ah.offers = new_offers_txs.1} in
  let s = transfer (s, new_offers_txs.2) in
  s
type finalize_args = {seed:nat; offer_ids:nat list}
type fa2_entry_points =
  | Transfer of transfer list
  | Balance_of of balance_of_param
  | Update_operators of update_operator list
  | Mint of transfer_destination list
  | Propose_tokens of propose_new_token_params list
  | Approve_reject_token_proposal of approve_reject_new_token_params
  | Post of offer list
  | Bid of (nat * nat) list
  | Finalize of finalize_args
let main (action, s : fa2_entry_points * storage) : operation list * storage =
 match action with
 | Transfer t -> let _ = if can_transfer t then () else failwith "FA2_NOT_OWNER" in transfer (s, t)
 | Mint m -> mint (s,m) 
 | Propose_tokens x -> propose_new_tokens (s,x)
 | Approve_reject_token_proposal x -> approve_reject_new_tokens (s,x)
 | Post p -> post (s, p)
 | Bid x -> bid (s, x)
 | Finalize f -> finalize (s, (f.seed, f.offer_ids))
 | _ -> failwith "Not implemented"
