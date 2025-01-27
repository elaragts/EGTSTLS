﻿using System.Buffers.Binary;
using GameDatabase.Context;
using Microsoft.Extensions.Options;
using TaikoLocalServer.Settings;
using Throw;

namespace TaikoLocalServer.Handlers;

public record UserDataQuery(uint Baid) : IRequest<CommonUserDataResponse>;

public class UserDataQueryHandler(TaikoDbContext context, IGameDataService gameDataService, ILogger<UserDataQueryHandler> logger, IOptions<ServerSettings> settings) 
    : IRequestHandler<UserDataQuery, CommonUserDataResponse>
{

    private readonly ServerSettings settings = settings.Value;

    public async Task<CommonUserDataResponse> Handle(UserDataQuery request, CancellationToken cancellationToken)
    {
        var userData = await context.UserData.FindAsync(request.Baid, cancellationToken);
        userData.ThrowIfNull($"User not found for Baid {request.Baid}!");
        
        var unlockedSongIdList = userData.UnlockedSongIdList;
        var unlockedUraSongIdList = userData.UnlockedUraSongIdList;

        var songIdMax = settings.EnableMoreSongs ? settings.MoreSongsSize : Constants.MusicIdMax;

        var musicList = gameDataService.GetMusicList();
        var lockedSongsList = gameDataService.GetLockedSongsList().Except(unlockedSongIdList).ToList();
        var lockedUraSongsList = gameDataService.GetLockedUraSongsList().Except(unlockedUraSongIdList).ToList();
        var enabledMusicList = musicList.Except(lockedSongsList);
        var releaseSongArray =
            FlagCalculator.GetBitArrayFromIds(enabledMusicList, songIdMax, logger);

        var defaultSongWithUraList = gameDataService.GetMusicWithUraList();
        var enabledUraMusicList = defaultSongWithUraList.Except(lockedUraSongsList);
        var uraSongArray =
            FlagCalculator.GetBitArrayFromIds(enabledUraMusicList, songIdMax, logger);

        if (userData.ToneFlgArray.Count == 0)
        {
            userData.ToneFlgArray = [0];
            context.UserData.Update(userData);
            await context.SaveChangesAsync(cancellationToken);
        }
        
        var toneArray = FlagCalculator.GetBitArrayFromIds(userData.ToneFlgArray, gameDataService.GetToneFlagArraySize(), logger);
        
        var titleArray = FlagCalculator.GetBitArrayFromIds(userData.TitleFlgArray, gameDataService.GetTitleFlagArraySize(), logger);

        var recentSongs = await context.SongPlayData
            .Where(datum => datum.Baid == request.Baid)
            .OrderByDescending(datum => datum.PlayTime)
            .ThenByDescending(datum => datum.SongNumber)
            .Select(datum => datum.SongId)
            .ToArrayAsync(cancellationToken);

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
        
        var defaultOptions = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(defaultOptions, userData.OptionSetting);

        uint[] difficultySettingArray = [userData.DifficultySettingCourse, userData.DifficultySettingStar, userData.DifficultySettingSort];
        for (int i = 0; i < 3; i++)
        {
            if (difficultySettingArray[i] >= 2)
            {
                difficultySettingArray[i] -= 1;
            }
        }
        
        var response = new CommonUserDataResponse
        {
            Result = 1,
            ToneFlg = toneArray,
            TitleFlg = titleArray,
            ReleaseSongFlg = releaseSongArray,
            UraReleaseSongFlg = uraSongArray,
            AryFavoriteSongNoes = userData.FavoriteSongsArray.ToArray(),
            AryRecentSongNoes = recentSongs,
            DefaultOptionSetting = defaultOptions,
            NotesPosition = userData.NotesPosition,
            IsVoiceOn = userData.IsVoiceOn,
            IsSkipOn = userData.IsSkipOn,
            DifficultySettingCourse = difficultySettingArray[0],
            DifficultySettingStar = difficultySettingArray[1],
            DifficultySettingSort = difficultySettingArray[2],
            DifficultyPlayedCourse = userData.DifficultyPlayedCourse,
            DifficultyPlayedStar = userData.DifficultyPlayedStar,
            DifficultyPlayedSort = userData.DifficultyPlayedSort,
            SongRecentCnt = (uint)recentSongs.Length,
            IsChallengecompe = false,
            // TODO: Other fields
        };

        return response;
    }
}