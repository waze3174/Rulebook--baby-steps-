# Rulebook (baby steps)

a melonloader mod for baby steps that lets you make your own rules. make the game easier, harder or just weird: grip, how much it takes to fall, gravity, fatigue, wind, the size of nate and each part of him, and whether he drops his stuff.

## what it does

a **RULEBOOK** page in the pause menu (needs BabyStepsMenuLib, see install), every slider shows its normal value:

- **falls**: lean limit, balance time, hard landings, splits
- **world**: grip (down to ice), gravity, fatigue, wind
- **arms**: head, upper arms, forearms, hands
- **legs**: torso, thighs, shins, feet
- **size**: whole nate, hips & butt
- **misc**: never drop items, never lose the hat
- **options**: show the rules on screen, reset everything to normal

gravity changes everything about nate: standing, walking, lifting his feet and falling, plus the world around him.

never drop items / never lose the hat stop him dropping what he holds and losing his hat when he falls or bumps his hands. swapping items and hats still works.

you can turn on a small tag in the bottom right that lists every rule that isn't normal (options).

body sizes are real, not just looks: the ragdoll, the visible body, how long his legs are for walking, his feet and the camera all change together. the game's walking only holds up from x0.5 to x1.15, so the parts that matter for walking (whole nate, torso, thighs, shins, feet, hips) stay in that range. head, arms and hands go further.

with my other mods falltracker or practicetools also installed, the pages open from one **MODS** button so the pause menu doesn't run off the screen.

pressing F9 (flyandquicksave's save key) no longer empties every mod's pause menu page.

## multiplayer

with the baby steps multiplayer mod, your body sizes are sent to other players who have rulebook too, so they see you at your size. players without rulebook see a stretched or squashed nate if you changed sizes. **this should work but hasn't been tested with other players yet.**

the rules (grip, falls, gravity...) only change your own nate, so everyone can play by their own rules.

## install

1. install [melonloader](https://melonwiki.xyz/) 0.7.x for baby steps
2. drop `Rulebook.dll` in `Baby Steps/Mods/`
3. optional, for the pause menu page: [BabyStepsMenuLib](https://github.com/caleborchard/BabyStepsMenuLib) (also comes with the multiplayer mod). without it, set everything in the cfg file

## settings

`Baby Steps/UserData/Rulebook/Rulebook.cfg` (made on first launch), every slider is in there with its range. if nate ever looks broken after updating, delete this file to start fresh.

## build

needs the .net 6 sdk and the game with melonloader installed (for the references). `BabyStepsMenuLib.dll`, `BabyStepsMultiplayerClient.dll` and `BabyStepsNetworking.dll` are expected in the game's `Mods` folder to build.

```
dotnet build src/Rulebook.csproj -c Release -p:GameDir="C:\path\to\Baby Steps"
```

## credits

- baby steps by gabe cuzzillo, maxi boch and bennett foddy
- pause menu page made with [BabyStepsMenuLib](https://github.com/caleborchard/BabyStepsMenuLib) by caleb orchard (not included, used if installed)
- multiplayer sizes go through the [baby steps multiplayer mod](https://github.com/caleborchard/Baby-Steps-Multiplayer-Mod-Client) by caleb orchard (not included, used if installed)

tested on baby steps (steam) with melonloader 0.7.3, on steam deck.

not affiliated with or endorsed by gabe cuzzillo, maxi boch, bennett foddy or devolver digital.

## licence

cc0 1.0, do whatever you want with it. see `LICENSE`.
