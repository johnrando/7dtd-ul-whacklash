# WhackLash

A 7 Days To Die mod. Keep hitting the same enemy and it starts to give.

Every enemy you hit gets a **focus meter**. Each hit you land adds to it and it drains steadily
from the moment of the last hit. While it is up, your hits on that enemy are worth more: they do
more damage, build towards a knockdown faster, dismember more often, and a knockdown can become a
full ragdoll. Five hits in a row are worth more than five hits over a minute — you are working a
priority target over, and the game now notices.

The game has a meter of its own that runs the other way. Vanilla's **pain meter** fills as a zombie
takes hits and, once full enough, lets it attack straight through yours and shrug off the slow a
flinch would cause — for nearly every zombie that happens on the second hit. WhackLash leaves that
alone until your focus meter reaches the **break point**, 3 points by default. Below it the zombie
gets tougher exactly as in vanilla, so the hits that build the meter are landed at risk. At it the
zombie breaks: its pain meter is held down, so every hit keeps it slowed and it
cannot attack through, for as long as you keep the meter up there — and its hits can start going
sideways, a stagger or a drop to a knee away from you instead of the stumble at you. With bare
fists at the defaults that is: hit one flinches, hits two and three the zombie swings back through,
hit four breaks it, and about four seconds without a hit lets it recover.

## Installing

Download the zip from Releases and extract it into the game's `Mods/`. The mod folder is the root
of the archive, so it lands as:

```
Mods/WhackLash/
├── ModInfo.xml
└── WhackLash.dll
```

Load order does not matter, and nothing needs building. In multiplayer, install it on the server
and on every client — each machine works out its own hits.

## How the meter works

A hit adds points by what landed it:

| Hit | Points |
|---|---|
| melee swing (and a thrown spear, which carries the same item) | 1 |
| arrow, bolt, grenade, molotov, anything thrown | 0.5 |
| bullet, launcher, explosive | 0.25 |
| turret, drone, trap, vehicle, burn, bleed, another zombie | 0 — earns nothing, builds nothing |

The meter holds at most 10 points and drains 0.2 a second, the same rate as the game's own pain
meter, so a full meter is gone 50 seconds after the last hit. Every bonus is a percentage **per point**, read off the meter as it stood
*before* the hit, so the first hit of a chain earns nothing and every hit after it earns off the
ones before.

Worked example: five quick machete hits. The first lands plain and puts the meter at 1. The
second, at 1 point, does +1% damage, builds +2% more towards a knockdown, and rolls dismember at
1.02x. The fifth, at 4 points, is +4%, +8%, 1.08x, and if it knocks the zombie down there is an 8%
chance that knockdown is a ragdoll. Stop for twenty seconds and you are back to the first hit.

At a full meter of 10 the defaults come to +20% knockdown build-up, 1.2x dismember chance, +10%
damage and a 20% ragdoll. Small steps, many of them: the meter is there to be kept up, not spiked.

**A head dismember is a kill.** The dismember bonus multiplies the weapon's own dismember chance,
and the game treats a decapitation as fatal, so on a weapon with a real head-dismember chance this
bonus is the strong one. `wl bonus` turns it down or off.

**Who takes part.** Zombies by default — everything the game flags as a zombie, which includes
zombie dogs, vultures, and Undead Legacy's own, plus bandits. Hostile animals — bears, wolves,
boars, mountain lions — are off by default and switched on with `wl animals on`. A bear you can
break so it stops attacking through your hits is a different animal, so that one is your call.

## Console commands

`wl` prints the menu and changes nothing — `whacklash` is an alias. Every line names the command
that changes it and says what it is for, so the menu is also the reference:

```
WhackLash is ON
  wl on|off               : [ >on< | off ]   - build a focus meter on enemies you keep hitting
  wl break {points}|off   : zombie stops attacking through at 3 points
  wl zombies on|off       : [ >on< | off ]   - zombies, zombie dogs and vultures build the meter
  wl animals on|off       : [ on | >off< ]   - hostile animals build the meter too
  wl weights {m} {a} {g}  : 1 melee / 0.5 archery+thrown / 0.25 gun+launcher per hit
  wl cap {points}         : meter tops out at 10 points
  wl decay {per sec}      : meter drains 0.2 points per second
  wl bonus {s} {d} {h} {r}: per point +2% knockdown, +2% dismember, +1% damage, 2% ragdoll on knockdown
  wl side {flinch} {fall} : once broken, per point 2% hit from the side, 1% dropped to a knee sideways
  wl door {pct} {min}     : a slammed door floors a zombie 10% per point, from 1 point up
  wl flavor ds            : [ >on< | off ]   - DoorSlammer: a slammed door can floor a zombie you have been working on
```

`wl on` and `wl off` are the master switch — with it off every hook returns immediately: no meter,
no bonuses, and the vanilla pain meter runs as normal. They say which state you want rather than
toggling, so the command reads the same whichever state you were in and repeating it is harmless.
`wl zombies` and `wl animals` work the same way.

`wl break {points}` sets the break point: the meter points a zombie needs before its pain meter is
held down and it stops attacking through your hits. 3 by default; 0 breaks it from the first hit,
which is a stun-lock, so use that knowingly. `wl break off` leaves the vanilla pain meter alone
throughout and keeps only the bonuses. Holding the meter down does not lengthen the flinch: the
pain the clamp takes off is banked, drains at vanilla's rate, and is put back for each hit, so a
broken zombie flinches for exactly as long as vanilla would have had it flinch, and does not fall
back into the long forward stumble after every knockdown, ragdoll or reload.

`wl side {flinch} {fall}` is what a broken zombie does instead of stumbling at you, each a
percentage per meter point. Every hit carries the direction it came from and the game plays the
flinch, the fall and the kneel to match. *Flinch* is the chance a straight-on hit is played as one
from the side, so the zombie staggers or falls sideways; *fall* is the chance a hit that would only
flinch drops it to a knee sideways instead, for the zombie's own kneel duration. Both apply only
once the zombie is broken. 0 switches either off.

`wl bonus` takes the four payoffs as percentages per meter point: knockdown build-up (as a share of
the hit's damage), dismember chance, damage, and the chance a knockdown becomes a ragdoll. 0
switches any one of them off. A setter called with no arguments prints its usage and current
value. **Changes are saved** — see [Settings file](#settings-file).

`wl info` prints the same block with the patch state and the counters added. The key line is
`hits`: the startup log only proves the hooks were installed, that number proves hits are reaching
them. `wl reset` zeroes the counters; live meters are left alone.

## DoorSlammer

With [DoorSlammer](../7dtd-ul-doorslammer) installed, a door slammed on a zombie you have been working
on can knock it down. The door reads the zombie's meter as it stood before the slam and rolls
`wl door`'s percentage per point — 10% per point from 1 point up by default, so a zombie you have
hit four times goes down two slams in five. The ragdoll roll applies to that knockdown too. The
slam then counts as one melee hit on the meter. A knockdown deals no damage and is credited to
nobody.

`wl flavor` lists the interactions, one switch per mod, and changes nothing. `wl flavor ds`
toggles the DoorSlammer pair; `wl flavor on` and `wl flavor off` set them all. Each pair is
switched **on both sides**, and toggling it in either one sets both: `wl flavor ds` and `ds flavor
wl` are the same switch. A mod this build does not know about is let through until you switch it
off, and gets a line of its own once it has been seen.

## Defaults

All settable in-game, and all written back to the settings file as soon as you set them. These are
what a first run starts from:

| Setting | Default |
|---|---|
| break point (pain meter held down from) | 3 points |
| zombies take part | on |
| hostile animals take part | off |
| points per hit: melee / archery+thrown / gun+launcher | 1 / 0.5 / 0.25 |
| meter cap | 10 points |
| decay | 0.2 points per second |
| knockdown build-up bonus | +2% per point |
| dismember chance bonus | +2% per point |
| damage bonus | +1% per point |
| ragdoll on knockdown | 2% per point |
| hit from the side, once broken | 2% per point |
| sideways drop to a knee, once broken | 1% per point |
| door knockdown (DoorSlammer) | 10% per point, from 1 point |
| mod interactions | on |

## Settings file

Every setting survives a restart. A change made with `wl` is written straight out to:

```
%APPDATA%/7DaysToDie/WhackLash/settings.txt
```

— the game's own user data folder, next to `Saves`, rather than `Mods/WhackLash/`, so updating
the mod does not take your settings with it. `wl info` prints the full path and whether the last
read or write worked.

It is plain `key = value` text, one line per setting, each naming the command that sets it:

```
enabled             = on       # wl on|off
break               = 3        # wl break {points}|off - meter points before the zombie stops attacking through; off leaves the vanilla pain meter alone
targets.zombies     = on       # wl zombies on|off
targets.animals     = off      # wl animals on|off
weight.melee        = 1        # wl weights {melee} {archery} {gun}
weight.archery      = 0.5      # wl weights {melee} {archery} {gun} - bows, crossbows, thrown
weight.gun          = 0.25     # wl weights {melee} {archery} {gun} - guns, launchers, explosives
cap                 = 10       # wl cap {points}
decay               = 0.2      # wl decay {points per second}
bonus.stun          = 2        # wl bonus {stun} {dismember} {damage} {ragdoll}
bonus.dismember     = 2        # wl bonus {stun} {dismember} {damage} {ragdoll}
bonus.damage        = 1        # wl bonus {stun} {dismember} {damage} {ragdoll}
bonus.ragdoll       = 2        # wl bonus {stun} {dismember} {damage} {ragdoll}
side.flinch         = 2        # wl side {flinch} {fall} - once broken, a straight-on hit played from the side
side.fall           = 1        # wl side {flinch} {fall} - once broken, a flinch becomes a sideways drop to a knee
door.percent        = 10       # wl door {pct} {min}
door.min            = 1        # wl door {pct} {min}
flavor.doorslammer  = on       # wl flavor ds
```

Edit it by hand with the game closed — it is rewritten whenever a `wl` command changes something.
A line that will not parse is logged and ignored rather than fatal, and deleting the file brings
back the defaults above (which live in `Settings.cs`).

## Multiplayer

Each machine works out its own hits — the damage, the knockdown, the dismember roll — and sends
the result to the server, and each keeps its own copy of every meter, fed by the same damage
traffic the server sees. So the settings on the machine that swung the weapon are the ones that
apply to that swing. Keep them the same everywhere. The console command runs on the server; on a
client it changes the server's settings, not your own, so edit the settings file for a client.

## Undead Legacy

**Not required** — the mod works on a plain install, and is built to sit alongside UL without
modifying anything of UL's. UL replaces the method that plays a hit on a zombie with a copy of its
own, but keeps the pain meter and knockdown maths intact, and this mod hooks around that copy
rather than into it. Tested against **UL 2.7.36**.

One UL interaction to know about: UL rolls a rage on every hit a zombie takes, with a chance that
scales with the hit's damage, and a raging zombie runs at you. The damage bonus here raises that
roll along with the damage, up to a tenth more at a full meter. If broken zombies seem to charge
more than you would like, `wl bonus` with the damage figure lowered is the knob.

## Limitations

- A **thrown spear** counts as melee, not archery: it carries the same item as a held one and the
  game gives no other handle.
- The **vanilla pain meter** is held just below the point where it works for the zombie rather
  than zeroed, so a mod that displays it (PainMeter) still shows it moving. PainMeter 0.0.0.2 and
  up also draws this meter under its bar: a pip per point, the break point framed, a locked tint
  once the zombie is broken, and the damage bonus beside it.
- A **sleeping zombie** builds the meter but gets no knockdown bonus until it is up — the game
  computes no knockdown for sleepers, and neither does this.

## Building

Requires the .NET SDK; there are no NuGet dependencies. The mod builds in place inside the game
install, against the game's own assemblies.

```
dotnet build src/WhackLash/WhackLash.csproj -c Release
```

That restages `dist/WhackLash/`, ready to copy into `Mods/`. To also build the release archive:

```
dotnet build src/WhackLash/WhackLash.csproj -c Release -t:Package
```

That writes `release/WhackLash-v<version>-<date>.zip`, taking the version from `ModInfo.xml`.
Neither `dist/` nor `release/` is tracked — the zip is published as a Release instead.

## License

MIT — see `LICENSE`.
