﻿//         DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
//                     Version 2, December 2004
//
//  Copyright (C) 2004 Sam Hocevar<sam@hocevar.net>
//
//  Everyone is permitted to copy and distribute verbatim or modified
//  copies of this license document, and changing it is allowed as long
//  as the name is changed.
//
//             DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
//    TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION
//
//   0. You just DO WHAT THE FUCK YOU WANT TO.
using MobiusEditor.Interface;

namespace MobiusEditor.Model
{
    public class MapLoadInfo
    {
        public string FileName;
        public FileType FileType;
        public IGamePlugin Plugin;
        public bool MapLoaded;
        public string[] Errors;

        public MapLoadInfo(string fileName, FileType fileType, IGamePlugin plugin, string[] errors, bool mapLoaded)
        {
            this.FileName = fileName;
            this.FileType = fileType;
            this.Plugin = plugin;
            this.MapLoaded = mapLoaded;
            this.Errors = errors;
        }

        public MapLoadInfo(string fileName, FileType fileType, IGamePlugin plugin, string[] errors)
            : this(fileName, fileType, plugin, errors, false)
        {
        }
    }
}
