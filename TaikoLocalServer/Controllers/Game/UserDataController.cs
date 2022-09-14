﻿using System.Buffers.Binary;
using System.Collections;
using System.Text.Json;
using TaikoLocalServer.Services.Interfaces;
using Throw;

namespace TaikoLocalServer.Controllers.Game;

[Route("/v12r03/chassis/userdata.php")]
[ApiController]
public class UserDataController : BaseController<UserDataController>
{
    private readonly IUserDatumService userDatumService;

    private readonly ISongPlayDatumService songPlayDatumService;
    
    public UserDataController(IUserDatumService userDatumService, ISongPlayDatumService songPlayDatumService)
    {
        this.userDatumService = userDatumService;
        this.songPlayDatumService = songPlayDatumService;
    }

    [HttpPost]
    [Produces("application/protobuf")]
    public async Task<IActionResult> GetUserData([FromBody] UserDataRequest request)
    {
        Logger.LogInformation("UserData request : {Request}", request.Stringify());

        var musicAttributeManager = MusicAttributeManager.Instance;

        var releaseSongArray = new byte[Constants.MUSIC_FLAG_ARRAY_SIZE];
        var bitSet = new BitArray(Constants.MUSIC_ID_MAX);
        foreach (var music in musicAttributeManager.Musics)
        {
            bitSet.Set((int)music, true);
        }
        bitSet.CopyTo(releaseSongArray, 0); 
        
        var uraSongArray = new byte[Constants.MUSIC_FLAG_ARRAY_SIZE];
        bitSet.SetAll(false);
        foreach (var music in musicAttributeManager.MusicsWithUra)
        {
            bitSet.Set((int)music, true);
        }
        bitSet.CopyTo(uraSongArray, 0);

        var userData = await userDatumService.GetFirstUserDatumOrDefault(request.Baid);

        var toneFlg = Array.Empty<uint>();
        try
        {
            toneFlg = JsonSerializer.Deserialize<uint[]>(userData.ToneFlgArray);
        }
        catch (JsonException e)
        {
            Logger.LogError(e, "Parsing tone flg json data failed");
        }

        // The only way to get a null is provide string "null" as input,
        // which means database content need to be fixed, so better throw
        toneFlg.ThrowIfNull("Tone flg should never be null!");

        var toneArray = new byte[Constants.TONE_UID_MAX];
        bitSet = new BitArray(Constants.TONE_UID_MAX);
        foreach (var tone in toneFlg)
        {
            bitSet.Set((int)tone, true);
        }
        bitSet.CopyTo(toneArray, 0);

        var titleFlg = Array.Empty<uint>();
        try
        {
            titleFlg = JsonSerializer.Deserialize<uint[]>(userData.TitleFlgArray);
        }
        catch (JsonException e)
        {
            Logger.LogError(e, "Parsing title flg json data failed");
        }

        // The only way to get a null is provide string "null" as input,
        // which means database content need to be fixed, so better throw
        titleFlg.ThrowIfNull("Title flg should never be null!");

        var titleArray = new byte[Constants.TITLE_UID_MAX];
        bitSet = new BitArray(Constants.TITLE_UID_MAX);
        foreach (var title in titleFlg)
        {
            bitSet.Set((int)title, true);
        }
        bitSet.CopyTo(titleArray, 0);

        var recentSongs = (await songPlayDatumService.GetSongPlayDatumByBaid(request.Baid))
            .AsEnumerable()
            .OrderByDescending(datum => datum.PlayTime)
            .ThenByDescending(datum => datum.SongNumber)
            .Select(datum => datum.SongId)
            .ToArray();
        
        // Use custom implementation as distinctby cannot guarantee preserved element
        var recentSet = new OrderedSet<uint>();
        foreach (var id in recentSongs)
        {
            recentSet.Add(id);
            if (recentSet.Count == 10)
            {
                break;
            }
        }

        recentSongs = recentSet.ToArray();

        var favoriteSongs = Array.Empty<uint>();
        try
        {
            favoriteSongs = JsonSerializer.Deserialize<uint[]>(userData.FavoriteSongsArray);
        }
        catch (JsonException e)
        {
            Logger.LogError(e, "Parsing favorite songs json data failed");
        }

        // The only way to get a null is provide string "null" as input,
        // which means database content need to be fixed, so better throw
        favoriteSongs.ThrowIfNull("Favorite song should never be null!");

        var defaultOptions = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(defaultOptions, userData.OptionSetting);
        
        var response = new UserDataResponse
        {
            Result = 1,
            ToneFlg = toneArray,
            TitleFlg = titleArray,
            ReleaseSongFlg = releaseSongArray,
            UraReleaseSongFlg = uraSongArray,
            DefaultOptionSetting = defaultOptions,
            IsVoiceOn = userData.IsVoiceOn,
            IsSkipOn = userData.IsSkipOn,
            IsChallengecompe = false,
            SongRecentCnt = (uint)recentSongs.Length,
            AryFavoriteSongNoes = favoriteSongs,
            AryRecentSongNoes = recentSongs,
            NotesPosition = userData.NotesPosition
        };

        return Ok(response);
    }
}