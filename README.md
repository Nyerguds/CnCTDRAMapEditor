## Note: I am no longer working on this project. This repository still exists for archival purposes and so that people can fork it and continue the project if they wish. 

### Old readme follows:

## C&C Tiberian Dawn and Red Alert Map Editor

An enhanced version of the C&C Tiberian Dawn and Red Alert Map Editor based on the source code released by Electronic Arts.
The goal of the project is simply to improve the usability and convenience of the map editor, fix bugs, improve and clean its code-base,
enhance compatibility with different kinds of systems and enhance the editor's support for mods.

Once the project has proceeded far enough and I am pleased with the state of the editor, I might also look into making it support Tiberian Sun.

### Current features

* Downsized menu graphics by an user-configurable factor so you can see more placeable object types at once on sub-4K monitors
* Improved zoom levels
* Fixed a couple of crashes
* Made tool windows remember their previous position, size and other settings upon closing and re-opening them
* Replaced drop-downs with list boxes in object type selection dialogs to allow switching between objects with fewer clicks 

This list will be kept up-to-date as more features are added.

### Installation and usage

To install, simply download a compiled build from the [Releases section of this repository](https://github.com/Rampastring/CnCTDRAMapEditor/releases)
and unzip the build into a new directory.
**Do not overwrite the original map editor that comes with the C&C Remastered Collection**. The map editor will ask for your game
directory on first launch and then load all assets from the specified directory.

If you wish to compile and run the map editor from source code, simply clone this repository and open it
with Visual Studio 2017 or later with support for .NET desktop development installed.

### Contributing

Contributions are welcome in the scope of the project. If there's a bug that you'd like to fix or functionality that you'd like to enhance, feel free to make an issue for discussing it or a pull request if you'd want to push code.

### Contact

You can find me and discuss features on the Assembly Armada's [Discord server](https://discord.gg/UnWK2Tw). Note that this project is not officially affiliated with [The Assembly Armada](https://github.com/TheAssemblyArmada), but their server has become a general hub for discussing the released C&C source code and C&C reverse-engineering efforts, which provides a fitting context for this map editor.
