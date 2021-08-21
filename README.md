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

Video Demonstration: **[https://youtu.be/QgI1jBgc-pI](https://youtu.be/QgI1jBgc-pI)**

Download: [https://ivan-tigan.itch.io/reckless](https://ivan-tigan.itch.io/reckless)

**Game Technology:**

The game runs with a very decentralized and fast algorithm developed by us, called Hermes. Hermes is an algorithm similar to Deterministic Lockstep where game logic is deterministic, players reach consensus on inputs, and then simulate game steps. However, Hermes is much faster (allowing fast real time games) while also solving some security issues of naive lockstep implementations without the performance penalties (slow consensus, secure randomness, and more.).

A very short explanation is that a central server is a sort of oracle for time, randomness, and input synchronization between the players. An important thing to note is that this server does not have any knowledge of the game. The game itself runs on each player’s machine, which makes it much more decentralized than standard Client-Server architectures.

**Crypto Technology:**

The assets used in the game include currencies, currently just one - Reckless Tokenx (REX), and other tokens representing customization items (axes, heads, torsos, legs, arms ). The tokens are implemented via an multiasset smart contract conforming to the FA2 interface according to the tzip-12 standard.

The Reckless Token is inflationary and is minted as a reward for players who play the game based on their performance. \
 \
Other items are minted once and them have a constant supply. For example, Bob draws an axe, make a proposal on the blockchain for some quantity of this axe to be minted. Once approved, the initial supply is distributed based on Bob’s proposal. The new owners are free to use or sell their new assets in the auction house. All metadata and images are stored on the sia skynet - a decentralized storage platform similar to IPFS but with incentive mechanisms. \
 \
The goal is to provide an infrastructure for artists and players to have a mutually beneficial interaction.

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

