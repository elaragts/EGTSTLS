﻿using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using TaikoLocalServer.Services.Interfaces;
using Throw;

namespace TaikoLocalServer.Controllers.Game;

using StageData = PlayResultDataRequest.StageData;

[Route("/v12r03/chassis/playresult.php")]
[ApiController]
public class PlayResultController : BaseController<PlayResultController>
{
    private readonly IUserDatumService userDatumService;

    private readonly ISongPlayDatumService songPlayDatumService;

    private readonly ISongBestDatumService songBestDatumService;

    private readonly IDanScoreDatumService danScoreDatumService;

    public PlayResultController(IUserDatumService userDatumService, ISongPlayDatumService songPlayDatumService,
        ISongBestDatumService songBestDatumService, IDanScoreDatumService danScoreDatumService)
    {
        this.userDatumService = userDatumService;
        this.songPlayDatumService = songPlayDatumService;
        this.songBestDatumService = songBestDatumService;
        this.danScoreDatumService = danScoreDatumService;
    }

    [HttpPost]
    [Produces("application/protobuf")]
    public async Task<IActionResult> UploadPlayResult([FromBody] PlayResultRequest request)
    {
        Logger.LogInformation("PlayResult request : {Request}", request.Stringify());
        var decompressed = GZipBytesUtil.DecompressGZipBytes(request.PlayresultData);

        var playResultData = Serializer.Deserialize<PlayResultDataRequest>(new ReadOnlySpan<byte>(decompressed));

        Logger.LogInformation("Play result data {Data}", playResultData.Stringify());

        var response = new PlayResultResponse
        {
            Result = 1
        };

        var lastPlayDatetime = DateTime.ParseExact(playResultData.PlayDatetime, Constants.DATE_TIME_FORMAT,
            CultureInfo.InvariantCulture);

        await UpdateUserData(request, playResultData, lastPlayDatetime);

        var playMode = (PlayMode)playResultData.PlayMode;

        if (playMode == PlayMode.DanMode)
        {
            await UpdateDanPlayData(request, playResultData);
            return Ok(response);
        }

        var bestData = await songBestDatumService.GetAllSongBestData(request.BaidConf);
        for (var songNumber = 0; songNumber < playResultData.AryStageInfoes.Count; songNumber++)
        {
            var stageData = playResultData.AryStageInfoes[songNumber];

            await UpdateBestData(request, stageData, bestData);

            await UpdatePlayData(request, songNumber, stageData, lastPlayDatetime);
        }
        
        return Ok(response);
    }

    private async Task UpdateDanPlayData(PlayResultRequest request, PlayResultDataRequest playResultDataRequest)
    {
        if (playResultDataRequest.IsNotRecordedDan)
        {
            Logger.LogInformation("Dan score will not be saved!");
            return;
        }

        var danScoreData =
            await danScoreDatumService.GetSingleDanScoreDatum(request.BaidConf, playResultDataRequest.DanId) ??
            new DanScoreDatum
            {
                Baid = request.BaidConf,
                DanId = playResultDataRequest.DanId
            };
        danScoreData.ClearState =
            (DanClearState)Math.Max(playResultDataRequest.DanResult, (uint)danScoreData.ClearState);
        danScoreData.ArrivalSongCount =
            Math.Max((uint)playResultDataRequest.AryStageInfoes.Count, danScoreData.ArrivalSongCount);
        danScoreData.ComboCountTotal = Math.Max(playResultDataRequest.ComboCntTotal, danScoreData.ComboCountTotal);
        danScoreData.SoulGaugeTotal = Math.Max(playResultDataRequest.SoulGaugeTotal, danScoreData.SoulGaugeTotal);

        UpdateDanStageData(playResultDataRequest, danScoreData);

        await danScoreDatumService.InsertOrUpdateDanScoreDatum(danScoreData);
    }

    private void UpdateDanStageData(PlayResultDataRequest playResultDataRequest, DanScoreDatum danScoreData)
    {
        for (var i = 0; i < playResultDataRequest.AryStageInfoes.Count; i++)
        {
            var stageData = playResultDataRequest.AryStageInfoes[i];

            var songNumber = i;
            var danStageData = danScoreData.DanStageScoreData.FirstOrDefault(datum => datum.SongNumber == songNumber,
                new DanStageScoreDatum
                {
                    Baid = danScoreData.Baid,
                    DanId = danScoreData.DanId,
                    SongNumber = (uint)songNumber
                });

            danStageData.HighScore = Math.Max(danStageData.HighScore, stageData.PlayScore);
            danStageData.ComboCount = Math.Max(danStageData.ComboCount, stageData.ComboCnt);
            danStageData.DrumrollCount = Math.Max(danStageData.DrumrollCount, stageData.PoundCnt);
            danStageData.GoodCount = Math.Max(danStageData.GoodCount, stageData.GoodCnt);
            danStageData.TotalHitCount = Math.Max(danStageData.TotalHitCount, stageData.HitCnt);
            danStageData.OkCount = Math.Min(danStageData.OkCount, stageData.OkCnt);
            danStageData.BadCount = Math.Min(danStageData.BadCount, stageData.NgCnt);

            var index = danScoreData.DanStageScoreData.IndexOf(danStageData);
            if (index == -1)
            {
                danScoreData.DanStageScoreData.Add(danStageData);
            }
        }
    }

    private async Task UpdatePlayData(PlayResultRequest request, int songNumber, StageData stageData,
        DateTime lastPlayDatetime)
    {
        var songPlayDatum = new SongPlayDatum
        {
            Baid = request.BaidConf,
            SongNumber = (uint)songNumber,
            GoodCount = stageData.GoodCnt,
            OkCount = stageData.OkCnt,
            MissCount = stageData.NgCnt,
            ComboCount = stageData.ComboCnt,
            HitCount = stageData.HitCnt,
            Crown = PlayResultToCrown(stageData),
            Score = stageData.PlayScore,
            ScoreRate = stageData.ScoreRate,
            ScoreRank = (ScoreRank)stageData.ScoreRank,
            Skipped = stageData.IsSkipUse,
            SongId = stageData.SongNo,
            PlayTime = lastPlayDatetime,
            Difficulty = (Difficulty)stageData.Level
        };
        await songPlayDatumService.AddSongPlayDatum(songPlayDatum);
    }

    private async Task UpdateUserData(PlayResultRequest request, PlayResultDataRequest playResultData,
        DateTime lastPlayDatetime)
    {
        var userdata = await userDatumService.GetFirstUserDatumOrNull(request.BaidConf);

        userdata.ThrowIfNull();

        userdata.Title = playResultData.Title;
        userdata.TitlePlateId = playResultData.TitleplateId;
        var costumeData = new List<uint>
        {
            playResultData.AryCurrentCostume.Costume1,
            playResultData.AryCurrentCostume.Costume2,
            playResultData.AryCurrentCostume.Costume3,
            playResultData.AryCurrentCostume.Costume4,
            playResultData.AryCurrentCostume.Costume5
        };
        userdata.CostumeData = JsonSerializer.Serialize(costumeData);

        var lastStage = playResultData.AryStageInfoes.Last();
        var option = BinaryPrimitives.ReadInt16LittleEndian(lastStage.OptionFlg);
        userdata.OptionSetting = option;
        userdata.IsSkipOn = lastStage.IsSkipOn;
        userdata.IsVoiceOn = lastStage.IsVoiceOn;
        userdata.NotesPosition = lastStage.NotesPosition;

        userdata.LastPlayDatetime = lastPlayDatetime;
        userdata.LastPlayMode = playResultData.PlayMode;
        await userDatumService.UpdateUserDatum(userdata);
    }

    private async Task UpdateBestData(PlayResultRequest request, StageData stageData,
        IEnumerable<SongBestDatum> bestData)
    {
        var bestDatum = bestData
            .FirstOrDefault(datum => datum.SongId == stageData.SongNo &&
                                     datum.Difficulty == (Difficulty)stageData.Level,
                new SongBestDatum
                {
                    Baid = request.BaidConf,
                    SongId = stageData.SongNo,
                    Difficulty = (Difficulty)stageData.Level
                });

        // Determine whether it is dondaful crown as this is not reflected by play result
        var crown = PlayResultToCrown(stageData);

        bestDatum.UpdateBestData(crown, stageData.ScoreRank, stageData.PlayScore, stageData.ScoreRate);

        await songBestDatumService.UpdateOrInsertSongBestDatum(bestDatum);
    }

    private static CrownType PlayResultToCrown(StageData stageData)
    {
        var crown = (CrownType)stageData.PlayResult;
        if (crown == CrownType.Gold && stageData.OkCnt == 0)
        {
            crown = CrownType.Dondaful;
        }

        return crown;
    }
}