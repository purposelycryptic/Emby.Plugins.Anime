Emby.Plugins.AnimeFix
=====================

A fork of the anime metadata provider for Emby Server, serving as an attempt at fixing the following issues:


### 1. AniDB Genre Tags no longer being fetched

 Anime Categories (the rough equivalent of Emby Genres) no longer exist in AniDB - as part of large-scale restructuring in 2013, all separate descriptive attribute constructs were merged into a single Tag structure using universal elements in their definitions. Tags are still divided by type of associated object, with both the old Anime Categories and Tags now unified as "Anime Tags". Definition is similar to old Tags - example:

```xml
<tags>
    <tag id="Int" parentid="Int" infobox="Bool" weight="Int" localspoiler="Bool" globalspoiler="Bool" verified="Bool" update="YYYY-MM-DD">
    <name>String</name>
    <description>Lorem Ipsum...</description>
    </tag>
</tags>
```

Fetching a series' Tags in place of Categories was relatively simple, but the new Tag System uses a tree structure featuring multiple levels of categorization tags to organize individual sub-tags, which are useless when fetched as individual tags without the tree structure. 

To eliminate these useless tags and keep only those helpful branch-tags relevant to the series, at the moment the use of the GenreCleaner Plugin is required, either using the included GenreCleaner.xml configuration file or copying the relevant content from it if you already have the plugin configured and don't want to start over. This file was created by manually going over the ~1,600 AniDB Anime Tags, and paring them down to a list of roughly 750 useful descriptive tags, then using GenreCleaner's mapping function to properly capitalize them. The Genres used by TheTVDB.com are also included. Eventually I hope to include this functionality within the Anime Plugin itself.

NOTE: Since it is far easier to remove unwanted Genres in GenreCleaner than it is to add additional ones, the list of allowed Genres includes essentially all non-junk Anime Tags from AniDB - depending on your preferences, you may wish to further trim it down.


### 2. The Star Rating being fetched for Series is currently the AniDB Weighted Rating, not the Average, creating discrepancies in value assigned to Star Rating compared to non-anime series

ratings.temporary currently contains the Average Rating value, and ratings.permanent contains the Weighted Rating value, possibly switching from respective temp to perm values on series completion.

The Weighted Rating is calculated based heavily on the assumption that most users are prone to exagerated ratings due to subconsciously dividing the 1-10 rating scale into two, with 5-10 representing having no value to maximum value, and 1-5 representing a maximum negative value up to having no value (effectively a scale from negative 5 to positive 5). The weighting algorithm is intended to normalize the votes across the full 1-10 scale in order to represent what the rating would have been had users made proper use of the full scale in the first place. 

In practice, the most common effect is for series with an average rating from 4-7 to drop roughly 2 points, series rated 2-4 or 7-8.5 to drop between 0-1.5 points (higher average = larger drop for 2-4, opposite for 7-8.5), and series outside the 2-8.5 range either staying the same or seeing a small increase. This is due to votes largely clustering in the 5-8 range, with a smaller cluster in the 1-2 range.

As other metadata sources (TMDB, TVDB) use a straight average, using the AniDB's Weighted Ratings creates a noticeable difference in the ratings of anime and regular series that users would expect to have similar ratings. As such I have modified the plugin to fetch the Average Rating instead.


### 3. The Rate at which the Plug-in fetched data from AniDB would sometimes result in IP Bans

AniDB has a very low threshold for repeated metadata requests, which, when crossed multiple times over a time period, will result in an IP Ban; while the Ban is short in duration (~5 minutes), each additional attempt while banned will add another 5 minutes, and eventually more. During a monthly library scan, I have experienced being banned for over a week. As such, I have increased the wait time between requests slightly in order to hopefully avoid this issue.



## Compiling and Testing

You must have a %MediaBrowserData% environment variable pointing to the server data folder of the Media Browser server. The plugin will be copied into the plugins folder when the project is successfully built.

The GenreCleaner.xml file should be placed in the `%AppData%\Roaming\Emby-Server\plugins\configurations` folder.
