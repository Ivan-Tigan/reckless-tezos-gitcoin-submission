var express = require('express');
var app = express();
const bodyParser = require('body-parser');
var cors = require('cors');
const axios = require('axios');
const http = require('http');
const https = require('https');
const fs = require('fs');
const queryString = require('query-string')
var morgan = require('morgan');
var Mutex = require('async-mutex').Mutex;
const path = require('path');

app.use(morgan('dev'));

const mutex = new Mutex();
//app.use(cors({origin: [/localhost/i]}));
app.use(cors());

const { createProxyMiddleware } = require('http-proxy-middleware');
app.use('/api', createProxyMiddleware({ 
    target: 'http://localhost:1235/', //original url
    changeOrigin: true, 
    //secure: false,
    onProxyRes: function (proxyRes, req, res) {
       proxyRes.headers['Access-Control-Allow-Origin'] = '*';
    }
}));

var bcrypt = require('bcrypt');

var cryptoJS = require('crypto-js');
var AES =  require('crypto-js/aes');
var SHA256 = require('crypto-js/sha256');
const { SSL_OP_SSLEAY_080_CLIENT_DH_BUG, EWOULDBLOCK } = require('constants');
const { log } = require('console');
const { request } = require('http');

app.use(bodyParser.urlencoded({ extended: true }));
app.use(bodyParser.json());
//app.use(bodyParser.raw({type: 'application/json'}));

var port = process.env.PORT || 1235;
var router = express.Router();

router.use(function(req, res){
    let url = req.url.split('?');
    res.sendFile(path.join(__dirname+'/' + url[0]));
});

app.use('/',router);

/*const options = {
  key: fs.readFileSync('/etc/letsencrypt/live/higher-order-games.net/privkey.pem'),
  cert: fs.readFileSync('/etc/letsencrypt/live/higher-order-games.net/cert.pem'),
  ca: fs.readFileSync('/etc/letsencrypt/live/higher-order-games.net/chain.pem', 'utf8')
};*/
http.createServer(app);

//https.createServer(options, app).listen(port);

app.listen(port);
console.log('Magin happens on port ' + port);
