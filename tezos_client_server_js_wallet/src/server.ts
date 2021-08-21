import express, { response } from "express";
import { compose, TezosToolkit } from "@taquito/taquito";
import { InMemorySigner, importKey } from '@taquito/signer';
import { localForger } from '@taquito/local-forging';
import * as querystring from 'querystring';
import cors from 'cors';

import axios from 'axios';
import { EEXIST } from "constants";
// import {BodyParser} from 'body-parser'

const Tezos = new TezosToolkit('https://api.tez.ie/rpc/florencenet');
const app = express();
const port = 34199; // default port to listen

app.use(cors());

/*import { createProxyMiddleware } from 'http-proxy-middleware';
app.use('/api', createProxyMiddleware({ 
    target: 'http://localhost:34199/', //original url
    changeOrigin: true, 
    //secure: false,
    onProxyRes: function (proxyRes, req, res) {
       proxyRes.headers['Access-Control-Allow-Origin'] = '*';
    }
}));*/

app.use(express.json());



// define a route handler for the default home page
app.get( "/login", ( req, res ) => {
    try{
        let priv_key = req.body.priv;
        let s = new InMemorySigner(priv_key)
        Tezos.setProvider({signer: s});
        s.publicKey().then((pub) => {
            s.publicKeyHash().then((pub_hash) => {
                console.log("Login");
                res.json({pub: pub, pub_hash: pub_hash, priv: priv_key});
            })
        })
    }
    catch(error){
        res.send("error");
    }
} );

app.post('/contract', ( req, res ) => {
    try{
        let address = req.body.Item1;
        Tezos.contract
            .at(address)
            .then((contract) => {
                return Tezos.signer.publicKeyHash().then((pub) => {
                    let func = req.body.Item2;
                    let args = req.body.Item3;      
                    console.log("1", args)             
                    let call = contract.methods[func](args)
                    console.log("schema", contract.parameterSchema.ExtractSignatures())
                    console.log("here")
                    console.log("sending: ", JSON.stringify(call.toTransferParams(), null, 2))
                    console.log("after")
                    return call.send();

                });
            })
            .then((op) => {
                console.log(`Waiting for ${op.hash} to be confirmed...`);
                return op.confirmation(1).then(() => op.hash);
            })
            .then((hash) => {
                console.log(`Operation injected: https://better-call.dev/florencenet/opg/${hash}/contents`);
                res.json({case:"Ok",fields:[hash]});
            })
            .catch((error) => {
                let msg = JSON.stringify(error)
                console.log("Error ", msg)
                res.json({case:"Error",fields:[msg]});
            });
    }
    catch(error){
        res.send("error");
    }
});

app.post('/get_metadata', (req, res) => {
    try{
        let map_id = req.body.map_id;
        axios.get(`https://api.florence.tzstats.com/explorer/bigmap/${map_id}/values?limit=50`)
            .then(function (response) {
                Promise.all(response.data.map(
                    // @ts-ignore
                    (el) => {
                    let token_id = el.value.token_id 
                    let s = el.value.token_info[""]
                    let s2 = decodeURIComponent(s.replace(/\s+/g, '').replace(/[0-9a-f]{2}/g, '%$&'));
                    //console.log("s", s2)
                    return axios.get(s2).then((r) => {/*console.log("r", r.data);*/return {Item1: token_id, Item2: r.data}}).catch(function (error) {return error;});
                })).then((response) => {
                    //console.log("res", response)
                    res.send(response)})
            })
            
            .catch(function (error) {
                console.log("Error: ", error)
                res.status(500).send(error);
            });
        }
    catch(error){
        res.send("error");
    }
});
 
app.post('/get_token_proposals', (req, res) => {
    try{
        let map_id = req.body.map_id;
        axios.get(`https://api.florence.tzstats.com/explorer/bigmap/${map_id}/values?limit=50`)
            .then(function (response) {
                Promise.all(response.data.map(
                    // @ts-ignore
                    async (r) => {
                    return await Promise.all(r.value.map(
                        // @ts-ignore
                        async  (el) => {
                            //console.log(el);
                            let initial_supply = await Promise.all(el.initial_supply.map(
                                // @ts-ignore
                                (iS) => {
                                    return {Item1: iS["0"], Item2: iS["1"]};
                                }
                            )).then((RR) => {
                                return RR;
                            });
                            let s = el.token_info;
                            let s2 = decodeURIComponent(s.replace(/\s+/g, '').replace(/[0-9a-f]{2}/g, '%$&'));
                            el.token_info = await axios.get(s2).then((r) => {return r.data;}).catch(function (error) {return "";});
                            el.initial_supply = await initial_supply;
                            //console.log(await el);
                            return await el;
                        }
                    )).then((R) => {
                        return ({Item1: r.key, Item2: R});});
                    }
                )).then((response) => {
                    console.log(response);
                    res.send(response)})
            })
            
            .catch(function (error) {
                console.log("Error: ", error)
                res.status(500).send(error);
            });
    }
    catch(error){
        res.send("error");
    }
});

app.post('/get_ledger', (req, res) => {
    try{
        let map_id = req.body.map_id;
        axios.get(`https://api.florence.tzstats.com/explorer/bigmap/${map_id}/values?limit=50`)
            .then(function (response) {
                Promise.all(response.data.map(
                    // @ts-ignore
                    (el) => {
                    return {Item1: {Item1: el.key["0"], Item2: el.key["1"]}, Item2: el.value}}
                )).then((response) => {
                    console.log("res", response)
                    res.send(response)})
            })
            
            .catch(function (error) {
                console.log("Error: ", error)
                res.status(500).send(error);
            });
        }
    catch(error){
        res.send("error");
    }
});

app.get('/get/:url', (req, res) => {
    try{
        let url = req.params.url;
        console.log(url);
        axios.get(url)
            .then(function (response) {
                res.send(response.data);})
            .catch(function (error) {
                console.log("Error: ", error)
                res.status(500).send(error);
            });
        }
    catch(error){
        res.send("error");
    }
});


/*app.post('/get_key_hash', (req, res) => {
    let map_id = req.body.Item1.map_id;
    let args = req.body.Item2;
    let parsed = querystring.stringify(args);
    axios.get(`https://api.florence.tzstats.com/explorer/bigmap/${map_id}/keys?` + parsed)
        .then(function (response) {
            Promise.all(response.data.map(
                // @ts-ignore
                (el) => {
                return {Item1: {Item1: el.key["0"], Item2: el.key["1"]}, Item2: el.value}}
            )).then((response) => {
                console.log("res", response)
                res.send(response)})
        })
        
        .catch(function (error) {
            console.log("Error: ", error)
            res.status(500).send(error);
        });
});*/

app.listen( port, () => {
    console.log( `server started at http://localhost:${ port }` );

} );