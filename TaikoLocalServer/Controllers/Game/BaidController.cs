﻿using System.Text.Json;
using TaikoLocalServer.Services.Interfaces;

namespace TaikoLocalServer.Controllers.Game;

[ApiController]
[Route("/v12r03/chassis/baidcheck.php")]
public class BaidController : BaseController<BaidController>
{
    private readonly IUserDatumService userDatumService;

    private readonly ICardService cardService;

    private readonly ISongBestDatumService songBestDatumService;

    private readonly IDanScoreDatumService danScoreDatumService;

    public BaidController(IUserDatumService userDatumService, ICardService cardService, 
        ISongBestDatumService songBestDatumService, IDanScoreDatumService danScoreDatumService)
    {
        this.userDatumService = userDatumService;
        this.cardService = cardService;
        this.songBestDatumService = songBestDatumService;
        this.danScoreDatumService = danScoreDatumService;
    }


    [HttpPost]
    [Produces("application/protobuf")]
    public async Task<IActionResult> GetBaid([FromBody] BAIDRequest request)
    {
        Logger.LogInformation("Baid request: {Request}", request.Stringify());
        BAIDResponse response;
        var card = await cardService.GetCardByAccessCode(request.AccessCode);
        if (card is null)
        {
            Logger.LogInformation("New user with access code {AccessCode}", request.AccessCode);
            var newId = cardService.GetNextBaid();

            response = new BAIDResponse
            {
                Result = 1,
                PlayerType = 1,
                ComSvrResult = 1,
                MbId = 1,
                Baid = newId,
                AccessCode = request.AccessCode,
                IsPublish = true,
                CardOwnNum = 1,
                RegCountryId = "JPN",
                PurposeId = 1,
                RegionId = 1
            };

            return Ok(response);
        }

        var baid = card.Baid;

        var userData = await userDatumService.GetFirstUserDatumOrDefault(baid);

        var songBestData = await songBestDatumService.GetAllSongBestData(baid);
        
        var achievementDisplayDifficulty = userData.AchievementDisplayDifficulty;
        if (userData.AchievementDisplayDifficulty == Difficulty.None)
        {
            achievementDisplayDifficulty = songBestData.Any(datum => datum.BestCrown >= CrownType.Clear) ? 
                songBestData.Where(datum => datum.BestCrown >= CrownType.Clear).Max(datum => datum.Difficulty) : 
                Difficulty.Easy;
        }

        var songCountData = songBestData.Where(datum => achievementDisplayDifficulty != Difficulty.UraOni ?
                                                   datum.Difficulty == achievementDisplayDifficulty : 
                                                   datum.Difficulty is Difficulty.Oni or Difficulty.UraOni).ToList();

        var crownCount = new uint[3];
        foreach (var crownType in Enum.GetValues<CrownType>())
        {
            if (crownType != CrownType.None)
            {
                crownCount[(int)crownType - 1] = (uint)songCountData.Count(datum => datum.BestCrown == crownType);
            }
        }

        var scoreRankCount = new uint[7];
        foreach (var scoreRankType in Enum.GetValues<ScoreRank>())
        {
            if (scoreRankType != ScoreRank.None)
            {
                scoreRankCount[(int)scoreRankType - 2] = (uint)songCountData.Count(datum => datum.BestScoreRank == scoreRankType);
            }
        }


        var costumeData = new List<uint>{ 0, 0, 0, 0, 0 };
        try
        {
            costumeData = JsonSerializer.Deserialize<List<uint>>(userData.CostumeData);
        }
        catch (JsonException e)
        {
            Logger.LogError(e, "Parsing costume json data failed");
        }
        if (costumeData == null || costumeData.Count < 5)
        {
            Logger.LogWarning("Costume data is null or count less than 5!");
            costumeData = new List<uint> { 0, 0, 0, 0, 0 };
        }

        var costumeFlag = new byte[10];
        Array.Fill(costumeFlag, byte.MaxValue);

        var danData = await danScoreDatumService.GetDanScoreDatumByBaid(baid);
        
        var maxDan = danData.Where(datum => datum.ClearState != DanClearState.NotClear)
            .Select(datum => datum.DanId)
            .DefaultIfEmpty()
            .Max();
        var gotDanFlagArray = FlagCalculator.ComputeGotDanFlags(danData);

        response = new BAIDResponse
        {
            Result = 1,
            PlayerType = 0,
            ComSvrResult = 1,
            MbId = 1,
            Baid = baid,
            AccessCode = request.AccessCode,
            IsPublish = true,
            CardOwnNum = 1,
            RegCountryId = "JPN",
            PurposeId = 1,
            RegionId = 1,
            MydonName = userData.MyDonName,
            Title = userData.Title,
            TitleplateId = userData.TitlePlateId,
            ColorFace = userData.ColorFace,
            ColorBody = userData.ColorBody,
            ColorLimb = userData.ColorLimb,
            AryCostumedata = new BAIDResponse.CostumeData
            {
                Costume1 = costumeData[0],
                Costume2 = costumeData[1],
                Costume3 = costumeData[2],
                Costume4 = costumeData[3],
                Costume5 = costumeData[4]
            },
            CostumeFlg1 = costumeFlag,
            CostumeFlg2 = costumeFlag,
            CostumeFlg3 = costumeFlag,
            CostumeFlg4 = costumeFlag,
            CostumeFlg5 = costumeFlag,
            LastPlayDatetime = userData.LastPlayDatetime.ToString(Constants.DATE_TIME_FORMAT),
            IsDispDanOn = userData.DisplayDan,
            GotDanMax = maxDan,
            GotDanFlg = gotDanFlagArray,
            GotDanextraFlg = new byte[20],
            DefaultToneSetting = userData.SelectedToneId,
            GenericInfoFlg = new byte[10],
            AryCrownCounts = crownCount,
            AryScoreRankCounts = scoreRankCount,
            IsDispAchievementOn = userData.DisplayAchievement,
            DispAchievementType = (uint)achievementDisplayDifficulty,
            IsDispAchievementTypeSet = true,
            LastPlayMode = userData.LastPlayMode,
            IsDispSouuchiOn = true,
            AiRank = 0,
            AiTotalWin = 0,
            Accesstoken = "123456",
            ContentInfo = GZipBytesUtil.GetGZipBytes(new byte[10])
        };

        return Ok(response);
    }

}