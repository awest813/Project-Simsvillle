![image](https://user-images.githubusercontent.com/6294155/29047328-2bb60b54-7bc3-11e7-8d88-cee5309495ed.png)

[Latest Pre-release](https://github.com/riperiperi/Simitone/releases/latest/) | [Download](https://github.com/riperiperi/Simitone/releases/latest/download/SimitoneWindows.zip)

Alternative C# Windows Frontend for The Sims 1, based off of FreeSO. http://freeso.org 
(***REQUIRES*** a legitimate copy of The Sims: Complete Collection)

![image](https://user-images.githubusercontent.com/6294155/68995217-112b2680-0883-11ea-9f92-1acc839a7ec0.png)

NOTE! Currently does not support the entire Fame career track, saving on vacation and a few other important things. While all objects run, many of them have bugs that can make certain lot types unplayable. For current development progress, see [this issue.](https://github.com/riperiperi/Simitone/issues/8)

*Only for Desktop Windows.* Other platforms cannot be officially supported.

# Building

This tree targets .NET 9 and will not compile until the FreeSO submodule is present.

```
git clone --recurse-submodules https://github.com/awest813/Project-Simsvillle.git
cd Project-Simsvillle
git submodule update --init --recursive
```

Open `Client/Simitone/Simitone.sln` in Visual Studio 2022 or later with the .NET 9 SDK, and build `Simitone.Windows`. A legitimate The Sims 1 install is required to run the game.

Double-click `Simitone.exe` to open the launcher. Choose the The Sims folder that contains `GameData\Behavior.iff`, then press Play. The folder, language, resolution, window, and renderer are remembered.

`-nolauncher` skips that window when the saved or detected folder is already valid. `-path`, `-dx`, `-gl`, `-3d`, `-aa`, and `-lang` still override the saved settings:

```
Simitone.exe -nolauncher -path"C:\Path\To\The Sims\"
```

# Purpose

On modern operating systems, The Sims has a few nagging issues that make it less than playable. Simitone can be seen as a tool to improve the playability of your existing installation of The Sims. Here are some features:

- Playable in 3D - using a combination of models generated from the 2D sprites and created by the community.
  - [Download the mesh pack here!](http://forum.freeso.org/forums/3d-remeshing.40/)
- Custom user interface that works at modern resolutions. Working on a more desktop oriented interface.
- Improved graphical performance, support for high resolutions and refresh rates.
- Custom lighting - directional lights with smooth falloffs and shadows using generated 3D meshes.
- *Volcanic*, a program which allows you to examine, modify and create new game objects. (from FreeSO)

# Why is it called Simitone?

Simitone -> Semitone -> musical term -> C# -> a note

Further questions can be directed at my PR manager, uh, ... burglar cop.
