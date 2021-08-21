# reckless-tezos-gitcoin-submission
<!-----
NEW: Check the "Suppress top comment" option to remove this info from the output.

Conversion time: 0.315 seconds.


Using this Markdown file:

1. Paste this output into your source file.
2. See the notes and action items below regarding this conversion run.
3. Check the rendered output (headings, lists, code blocks, tables) for proper
   formatting and use a linkchecker before you publish this page.

Conversion notes:

* Docs to Markdown version 1.0β30
* Sat Aug 21 2021 11:47:47 GMT-0700 (PDT)
* Source doc: Reckless x Tezos
----->


**Introduction:**

Reckless is a fast-paced action combat arena video-game. Throw axes and slice the limbs off of your opponents. Run, dodge, dash and pick up your lost limbs from the ground to recover. Play short and intense battles, participate in grand tournaments, or invade other arenas and kill everyone with a whirlwind of deadly axes.

Play to earn Reckless Points (REX). Customize your axes and each of your body parts by purchasing items from the Tezos-powered decentralized auction house.

Artists have the unique opportunity to propose and then sell new customization options drawn by them directly in the in game auction house.

All assets are represented inside a Tezos FA2 multi-asset smart contract to guarantee ownership, security, and transparency for all transactions..

0% pay-to-win.  
100% fun-to-play.  
200% skill-to-win.  
300% badass.  

Video Demonstration: [https://youtu.be/QgI1jBgc-pI](https://youtu.be/QgI1jBgc-pI)  
Website: [http://higher-order-games.net:1235/index.html](http://higher-order-games.net:1235/index.html)  
Download: [https://ivan-tigan.itch.io/reckless](https://ivan-tigan.itch.io/reckless)  
Smart Contract: KT1Mw7E46UuQk62imBoYzTSUCpuz3LLXZ7qo

**Gameplay**
Combat in Reckless involves throwing axe to cut off the limbs of your opponent. The axes ricochet off walls and mercilessly cut everything in their path including the original thrower. Holding down the throw button forces you to stay still but charges up an incredibly powerful throw. Pressing the throw button just before an axe slices you allows you to catch it and instantly boost its power if you decide to throw it back. If you get cut by an axe you lose on of your limbs (left arm, right arm, left leg, right leg, head). You need arms to throw axes, you need legs to walk and dash quickly, you need your head to stay alive. When a limb is on the ground you can go, pick it up and attach it to yourself. The head can fall only if all of your other limbs have already been cut. Losing your head is game over. Dashing has multiple uses - you can defensively dodge out of danger's way. You can offensively dash towards you opponent to shorten the distance. You can even dash towards the trajectory of an axe to intercept it, catch it and throw it even stronger. A skilled player can craft a whirlwind of deadly axes and combo down their opponent.

Matches can be played with 2,3,4 players and tournaments can be played with up to 16 players.
THe most unique feature of reckless; however, is the ability to do invasions. You can spy on an arena and decide to hop in when the fighter are harmed to finish the off and earn big rewards. These mechanic create tense and rewarding situations where the best players have the chance to overcome all odds.

**Game Technology:**

The game runs with a very decentralized and fast algorithm developed by us, called Hermes. Hermes is an algorithm similar to Deterministic Lockstep where game logic is deterministic, players reach consensus on inputs, and then simulate game steps. However, Hermes is much faster (allowing fast real time games) while also solving some security issues of naive lockstep implementations without the performance penalties (slow consensus, secure randomness, and more.).

A very short explanation is that a central server is a sort of oracle for time, randomness, and input synchronization between the players. An important thing to note is that this server does not have any knowledge of the game. The game itself runs on each player’s machine, which makes it much more decentralized than standard Client-Server architectures.

Hermes is explained in depth in the original paper - [Hermes: Effective Real-Time Online Video Game Synchronization](https://drive.google.com/file/d/1SrcdhGj6ZgR6Ixq-6w3ICKykAN7wVkss/view?usp=sharing)

**Crypto Technology:**

The assets used in the game include currencies, currently just one - Reckless Tokenx (REX), and other tokens representing customization items (axes, heads, torsos, legs, arms ). The tokens are implemented via an multiasset smart contract conforming to the FA2 interface according to the tzip-12 standard.

The Reckless Token is inflationary and is minted as a reward for players who play the game based on their performance.

Other items are minted once and them have a constant supply. For example, Bob draws an axe, make a proposal on the blockchain for some quantity of this axe to be minted. Once approved, the initial supply is distributed based on Bob’s proposal. The new owners are free to use or sell their new assets in the auction house. All metadata and images are stored on the sia skynet - a decentralized storage platform similar to IPFS but with incentive mechanisms. \
 
Players can sell assets in extremely flexible way including as bundles and even mystery boxes.

Our goal is to provide an infrastructure for artists and players to have a flexible, fund, and mutually beneficial interaction.


**Roadmap:**

We want to deliver a very high quality product.

We are looking forward to working with the community and partnering with Tezos related organizations to deliver the best experience for everyone involved.

Here is a roadmap for our plans:

**2021 Q3**: Security:

Implement upgradable contracts.

Hermes validators implementation as defined in the paper.

**2021 Q4:** Testing

Playtesting and getting feedback from the community

Auction of exclusive items (FA2 assets) that will never be created again.

**2021 Q4 - 2022 Q1:** Release

Releasing the first iteration of the game

Marketing

**Future Work and Research:**

Improve decentralization:

Governance for item proposals and other game features.

General Tokenized Game Mods - the ability to create a mod/dlc, propose it on the blockchain and either sell it or get rewards when someone plays it

**Made by Higher Order Games**
 - Ivan Tsoninski - code
 - Mircea-Andrei Radu - code
 - Liutauras Kavaliauskas - art
