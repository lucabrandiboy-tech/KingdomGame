# Credits and licenses

Every asset or file we didn't make ourselves goes here **before** it is
committed: where it came from and its license. Only use assets whose license
allows commercial use (the game will be sold on Steam).

## Art and audio

| Asset | Made by | Source | License | Used for |
|---|---|---|---|---|
| `Assets/Art/Test/TestBrickWall.png` | Drawn by code in this repo (`TestSpriteMaker.cs`) | This repo | Ours | Phase 0 pixel and Git LFS test |

## Code and config

| File | Source | License |
|---|---|---|
| `.gitignore` (Unity part) | [github/gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore) | CC0-1.0 |
| `.gitattributes` (based on) | [alexkaratarakis/gitattributes](https://github.com/alexkaratarakis/gitattributes/blob/master/Unity.gitattributes) | MIT |
| `Assets/ThirdParty/LiteNetLib/` (networking: reliable UDP) | [RevenantX/LiteNetLib](https://github.com/RevenantX/LiteNetLib) 1.3.5 (commit 2c01ac0), source unchanged; its `LICENSE.txt` is next to it | MIT |

## Unity packages

Packages installed through Unity's Package Manager (URP, Input System, 2D
packages) come with Unity under the Unity Companion License and don't need to
be listed. Third-party code (like LiteNetLib above) does.
