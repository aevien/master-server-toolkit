namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Identifiers of game service platforms (grouped by platform type).
    /// NOTE: Existing numeric values are preserved where services were already shipped.
    /// </summary>
    public enum GameServiceId
    {
        #region PC / Desktop Stores & Launchers

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: Steam (Steamworks): auth, achievements, leaderboards, cloud saves, DLC, Workshop.
        /// Build targets: Windows (x64), optionally Linux/macOS.
        /// </summary>
        Steam = 7,

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: Epic Games Store: auth, achievements, cloud saves, IAP; distribution via EGS.
        /// Build targets: Windows (x64).
        /// </summary>
        EpicGamesStore = 8,

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: GOG (via GOG Galaxy): achievements, cloud saves, updates; DRM-free distribution.
        /// Build targets: Windows (x64).
        /// </summary>
        Gog = 9,

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: Microsoft Store (Windows): MS accounts, IAP, cloud saves, updates (UWP/Win32).
        /// Build targets: Windows (UWP/Win32).
        /// </summary>
        MicrosoftStore = 10,

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: VK Play (RU): VK ID sign-in, leaderboards/achievements, IAP; desktop client.
        /// Build targets: Windows (x64); sometimes web builds.
        /// </summary>
        VKPlay = 5,

        /// <summary>
        /// Category: PC & Web (indie store).
        /// Brief: Itch.io: distribution of downloads/demos, web builds, keys; simple APIs/payments.
        /// Build targets: Windows/macOS/Linux and WebGL.
        /// </summary>
        Itch = 4,

        /// <summary>
        /// Category: PC / Desktop.
        /// Brief: Local standalone build without an external store or launcher identity.
        /// Build targets: Windows/macOS/Linux standalone.
        /// </summary>
        Desktop = 1,

        #endregion

        #region Consoles

        /// <summary>
        /// Category: Console.
        /// Brief: Xbox (ID@Xbox): Xbox Live, achievements, leaderboards, cloud saves, IAP.
        /// Build targets: Xbox Series / One.
        /// </summary>
        Xbox = 11,

        /// <summary>
        /// Category: Console.
        /// Brief: PlayStation Store (PSN): trophies, PSN services, cloud saves, IAP.
        /// Build targets: PS4 / PS5.
        /// </summary>
        PlayStationStore = 12,

        /// <summary>
        /// Category: Console.
        /// Brief: Nintendo eShop: Nintendo accounts, NSO/cloud features, IAP.
        /// Build targets: Nintendo Switch.
        /// </summary>
        NintendoEShop = 13,

        #endregion

        #region Mobile Stores (iOS / Android)

        /// <summary>
        /// Category: Mobile.
        /// Brief: Apple App Store (iOS/iPadOS): Sign in with Apple, IAP, Game Center.
        /// Build targets: iOS / iPadOS.
        /// </summary>
        AppleAppStore = 14,

        /// <summary>
        /// Category: Mobile.
        /// Brief: Google Play: Google Play Games/Services (auth, leaderboards), Billing, cloud.
        /// Build targets: Android.
        /// </summary>
        GooglePlay = 15,

        /// <summary>
        /// Category: Mobile.
        /// Brief: Huawei AppGallery: HMS Core (auth, IAP, analytics) without GMS.
        /// Build targets: Android (HMS).
        /// </summary>
        HuaweiAppGallery = 16,

        /// <summary>
        /// Category: Mobile.
        /// Brief: Samsung Galaxy Store: IAP and distribution for Samsung devices.
        /// Build targets: Android.
        /// </summary>
        SamsungGalaxyStore = 17,

        /// <summary>
        /// Category: Mobile.
        /// Brief: Amazon Appstore (Android/Fire OS): IAP, distribution for Amazon/Fire devices.
        /// Build targets: Android / Fire OS.
        /// </summary>
        AmazonAppstore = 18,

        /// <summary>
        /// Category: Mobile.
        /// Brief: RuStore (RU): local store; VK ID / local payments and SDKs.
        /// Build targets: Android.
        /// </summary>
        RuStore = 19,

        #endregion

        #region Web / HTML5 Portals & Platforms

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: PlayWeb3: web3-oriented gaming; wallets/tokens; web SDK.
        /// Build targets: HTML5 / WebGL.
        /// </summary>
        PlayWeb3 = 2,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Yandex Games: Web SDK (auth, payments, leaderboards, ads/rewarded).
        /// Build targets: HTML5 / WebGL.
        /// </summary>
        YandexGames = 3,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Local WebGL build without an external game portal identity.
        /// Build targets: HTML5 / WebGL.
        /// </summary>
        Web = 32,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Game Jolt: community & distribution; web builds and downloads; basic APIs.
        /// Build targets: HTML5/WebGL, plus desktop builds.
        /// </summary>
        GameJolt = 20,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Newgrounds: web portal with API (medals, leaderboards); HTML5 hosting.
        /// Build targets: HTML5 / WebGL.
        /// </summary>
        Newgrounds = 21,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Kongregate: HTML5 portal; SDK (achievements/leaderboards/monetization).
        /// Build targets: HTML5 / WebGL.
        /// </summary>
        Kongregate = 22,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: Poki: HTML5 publisher/portal; ads/analytics SDK; strict UX/retention guidelines.
        /// Build targets: HTML5.
        /// </summary>
        Poki = 23,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: CrazyGames: HTML5 portal; SDK for monetization/analytics; metadata editor.
        /// Build targets: HTML5.
        /// </summary>
        CrazyGames = 24,

        /// <summary>
        /// Category: Web / HTML5 (aggregator).
        /// Brief: GameDistribution (Azerion): syndication across portal network; ads SDK.
        /// Build targets: HTML5.
        /// </summary>
        GameDistribution = 25,

        /// <summary>
        /// Category: Web / HTML5 (aggregator).
        /// Brief: GamePix: HTML5 distribution network; monetization/analytics SDK.
        /// Build targets: HTML5.
        /// </summary>
        GamePix = 26,

        /// <summary>
        /// Category: Web / HTML5.
        /// Brief: GX.games (Opera GX): distribution to Opera GX audience; simple SDKs.
        /// Build targets: HTML5.
        /// </summary>
        GXGames = 27,

        #endregion

        #region Social / Mini-Apps

        /// <summary>
        /// Category: Social browser games.
        /// Brief: VK Games: signed VK identity, storage, advertising, sharing, and VK Bridge APIs.
        /// Build targets: WebGL hosted as a VK Games application.
        /// </summary>
        VKGames = 6,

        #endregion

        #region VR / XR Stores & Distribution

        /// <summary>
        /// Category: VR.
        /// Brief: Meta Quest Store: curated store; Meta platform services (auth, SDKs).
        /// Build targets: Meta Quest / Quest 2 / Quest 3 / Pro.
        /// </summary>
        MetaQuestStore = 28,

        /// <summary>
        /// Category: VR (early access).
        /// Brief: Meta App Lab: early-access/experimental releases outside the main store.
        /// Build targets: Meta Quest family.
        /// </summary>
        MetaAppLab = 29,

        /// <summary>
        /// Category: VR (sideloading).
        /// Brief: SideQuest: alternative distribution/sideloading for Meta Quest.
        /// Build targets: Meta Quest family.
        /// </summary>
        SideQuest = 30,

        /// <summary>
        /// Category: VR.
        /// Brief: Pico Store: official store for Pico devices; Pico SDK/services.
        /// Build targets: Pico headsets.
        /// </summary>
        PicoStore = 31,

        #endregion

        #region Internal / Custom Services

        /// <summary>
        /// Category: Internal / Test.
        /// Brief: Editor-only stub for local testing.
        /// Build targets: Editor-only.
        /// </summary>
        Editor = 0,

        #endregion
    }
}
