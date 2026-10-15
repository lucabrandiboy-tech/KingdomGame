# Testing the new kinds of zombie

Run **KingdomGame > Build Game Scenes** first (it adds the new kinds to your waves and draws their
pictures). Nothing here has been run in Unity yet.

## The kinds

| Zombie | First wave | What it does | How to beat it |
|---|---|---|---|
| Walker | 1 | (as before) | |
| Runner | 3 | (as before) fast and weak | |
| Crawler | 2 | drags itself along the ground: slow, small, comes in numbers | anything; traps work well |
| Brute | 5 | (as before) tough, smashes walls | |
| Screamer | 4 | pale and quick; when it sees someone it **screams**: a pale red ring goes out and every zombie within 9 tiles turns reddish and runs 1.5 times as fast for 5 seconds (every 10 seconds) | kill it first, from far away |
| Spitter | 6 | green, with a swollen throat; **spits acid** at anyone up to 6.5 tiles away (12 damage where it lands), **even guards up on walls and towers**. The acid lands where you stood, so you can step out of the way | long-range towers, or shoot it before it gets close |
| Bloater | 7 | slow and swollen; when it reaches a wall, a door or a person it swells up (flashes yellow) and **bursts**: a blast that cracks walls and hurts people and other zombies round it. It also bursts when it's killed | kill it far from your walls |
| Armored | 9 | helmet and breastplate: **every hit does 6 less** (but always at least a fifth). Small quick shots (gatling, slings) do little; big shots, cannons and blasts hurt it; fire, gas and burning or spiked ground get through its armor | heavy hitters, fire |
| Giant | 12 | huge (twice a brute), very slow, 1100 health, smashes walls fast; rare | everything you have |

Zombie bases and their sieges now send the kinds the waves have got up to (no giants on day one).

## How to try them quickly

In **Free build**, press **F9**: 8 zombies come, one of each kind in turn (press it again for the
next ones). Careful, the bloater and the giant break things.

## What to check

- [ ] Each kind looks different (a crawler low on the ground with its legs dragging, a green
      spitter, a swollen bloater with yellow boils, a pale screamer with long hair, a grey
      helmet and breastplate on the armored one, a giant twice a brute's size)
- [ ] Spitter: a green glob flies at you in an arc and splashes; standing still you get hurt,
      stepping away you don't. A guard on a tower gets spat at too
- [ ] Screamer: a ring goes out, nearby zombies turn reddish and speed up for a few seconds
- [ ] Bloater: at a wall it flashes yellow and bursts; the wall gets a hole or cracks. Shot from
      afar it bursts out there. Nearby zombies get hurt by it too
- [ ] Armored: gatling hits do much less; a cannon or a rocket still hurts it a lot; the flamethrower bunker and gas traps hurt it as much as any zombie
- [ ] Giant: slow, takes a lot of shooting, and drops a big loot bag
- [ ] The survival waves bring the new kinds from their first waves on (see the table)
- [ ] Nothing gets stuck: zombies that can't reach anyone still wander off after a while

The numbers (health, speed, first wave, how common, the spit, the scream, the burst, the armor) are
on each kind in `Assets/Data/Zombies` and can be changed in the Inspector.
