# Testing the era weapons

Not run in Unity yet. Run **KingdomGame > Build Game Scenes** first: it draws the new
weapon pictures and adds the Armory Config.

## What changed

- The ruler starts with a **club** (left mouse button, hold to keep swinging): it hits every
  zombie in front within reach. The old rifle is gone.
- One weapon per age, made at an **anvil** (Furniture tab) by villagers with the new
  **Crafter** job (V):

  | Age | Weapon | Notes |
  |---|---|---|
  | Stone Age | Club (you start with it), Sling | |
  | Bronze Age | Bow | |
  | Iron Age | Crossbow | bolts go through one zombie |
  | Medieval | Hand cannon | one huge, wild shot |
  | Renaissance | Musket | |
  | Industrial | Lever-action rifle, Dynamite (G) | |
  | World War 1 | Bolt-action rifle | goes through two zombies |
  | World War 2 | Submachine gun (hold to fire), Bazooka (right mouse button) | |

- Walk up to an anvil and press **E**: the weapons screen. **Order it** takes the cost from
  storage (later ones need copper, iron and gold from the mines). Crafters make one weapon at
  a time; when it's done it goes into your hand.
- **Q** takes the next weapon, **1-9** picks one. The top left shows what's in your hand.
- In **free build** you have every weapon from the start.

## How to test

1. Survival: build an anvil (Furniture tab), give a villager the Crafter job (V).
2. E at the anvil: order a Sling. Watch the crafter walk to the anvil; after about 25 seconds
   of work it's ready and in your hand. Q switches back to the club.
3. Later weapons say "Needs the Bronze Age" and so on until you research that age.
4. Free build: press 1 to 9 and Q to go through all the weapons, F9 for a test horde to try them on.
5. Save and load: what you have and what's on order are kept.
