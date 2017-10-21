using System;
using System.Collections.Generic;
using System.Diagnostics;
using MediaBrowser.Model.Plugins;

namespace MediaBrowser.Plugins.Anime.Configuration
{
    public enum TitlePreferenceType
    {
        /// <summary>
        /// Use titles in the local metadata language.
        /// </summary>
        Localized,

        /// <summary>
        /// Use titles in Japanese.
        /// </summary>
        Japanese,

        /// <summary>
        /// Use titles in Japanese romaji.
        /// </summary>
        JapaneseRomaji,
    }

    public class PluginConfiguration
        : BasePluginConfiguration
    {
        public TitlePreferenceType TitlePreference { get; set; }
        public bool AllowAutomaticMetadataUpdates { get; set; }
        public bool TidyGenreList { get; set; }
        public int MaxGenres { get; set; }
        public bool AddAnimeGenre { get; set; }
        public bool UseAnidbOrderingWithSeasons { get; set; }

        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.JapaneseRomaji;
            AllowAutomaticMetadataUpdates = true;
            TidyGenreList = false;
            MaxGenres = 0;
            AddAnimeGenre = true;
            UseAnidbOrderingWithSeasons = false;
        }
    }
}
