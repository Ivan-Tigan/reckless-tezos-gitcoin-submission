import { TezosToolkit } from "@taquito/taquito";
import $ from "jquery";
import { TempleWallet } from '@temple-wallet/dapp';

export class App {
  private tk: TezosToolkit;

  constructor() {
    this.tk = new TezosToolkit("https://api.tez.ie/rpc/florencenet");
  }
  
  public async login() {
    TempleWallet.isAvailable()
    .then(() => {
      const mywallet = new TempleWallet('MyAwesomeDapp');
      mywallet.connect('florencenet').then(() => {
        this.tk.setWalletProvider(mywallet);
        return mywallet.getPKH()}).then((pkh) => {
          parent.postMessage({call: "setPubKeyHash", data: {pkh: pkh}}, "*");
      });
    })
    .catch((err) => parent.postMessage({call: "error", data: {error: "Error"}}, "*"));
  }

  public initUI() {
    let queryString = window.location.href;
    let str = queryString.split("=");
    let obj = JSON.parse(decodeURI(str[1]));
    let address = obj.address;
    let func = obj.func; 
    let args = obj.args;
    if(address == "login")
      this.login();
    else
      this.contract(address, func, args);
  }

  private contract(address: string, func: string, args: object) {
    this.login().then(() => {
      this.tk.wallet
      .at(address)
      .then((contract) => {
        return contract.methods[func](args).send();
      })
      .then((op) => {
        return op.confirmation().then(() => op.opHash)
      })
      .then((h) => {  parent.postMessage({call: "successTrans", data: {args: args, h: h}}, "*"); return h})
      .catch((e) => { parent.postMessage({call: "errorTrans", data: {args: args, e: e}}, "*");})
    })
  }
}
