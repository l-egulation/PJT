using ForestCombatV2;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        Deviant.Tutorial.Unity.DemoCompletionPanel demoCompletion;

        void BuildDemoCompletion()
        {
            if (!chapter2) return;
            demoCompletion = Deviant.Tutorial.Unity.DemoCompletionPanel.Create(MenuStage,
                () => Chapter1RunHost.Ensure().GoToVillage(),
                () => Chapter1RunHost.Ensure().GoToTitle());
            RegisterScreen(demoCompletion.gameObject, demoCompletion.VillageButton);
        }

        void DiscoverChapterTwo()
        {
            chapter2.ExternalInput=true;chapter2.ExternalPresentation=true;
            if(chapter2.diagnostics)chapter2.diagnostics.ExternalHud=true;
            shop=CommonMaintenanceShop.For(chapter2);shop.ExternalInput=true;shop.ExternalPresentation=true;
            map=chapter2.FullMap;if(map){map.ExternalPresentation=true;map.ExternalInput=true;}
            // 봉인 도전방·잠긴 상자방 지도 아이콘과 열쇠 HUD(stage-gimmicks.md)는 1챕터와 같은 구성품을 읽는다.
            sealChallenge=chapter2.Seal;keyChest=chapter2.Keys;
            if(!feedback&&host&&host.Arena)feedback=host.Arena.GetComponent<Deviant.Combat.CombatFeelFeedback>();
            if(feedback)feedback.ExternalHud=true;
        }
    }
}
