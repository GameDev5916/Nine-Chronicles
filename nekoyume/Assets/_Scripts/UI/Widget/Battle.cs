using System.Linq;
using Nekoyume.Game.Controller;
using Nekoyume.Game.VFX;
using Nekoyume.L10n;
using Nekoyume.Model.Mail;
using Nekoyume.State;
using Nekoyume.UI.Module;
using Nekoyume.UI.Scroller;
using TMPro;
using UnityEngine;

namespace Nekoyume.UI
{
    public class Battle : Widget
    {
        [SerializeField]
        private TextMeshProUGUI stageText = null;

        [SerializeField]
        private GuidedQuest guidedQuest = null;

        [SerializeField]
        private BossStatus bossStatus = null;

        [SerializeField]
        private Toggle repeatToggle = null;

        [SerializeField]
        private Toggle exitToggle = null;

        [SerializeField]
        private HelpButton helpButton = null;

        [SerializeField]
        private BossStatus enemyPlayerStatus = null;

        [SerializeField]
        private StageProgressBar stageProgressBar = null;

        [SerializeField]
        private ComboText comboText = null;

        [SerializeField]
        private GameObject boostEffectObject = null;

        [SerializeField]
        private TMP_Text boostCountText;

        public BossStatus BossStatus => bossStatus;
        public BossStatus EnemyPlayerStatus => enemyPlayerStatus;
        public StageProgressBar StageProgressBar => stageProgressBar;
        public ComboText ComboText => comboText;
        public const int RequiredStageForExitButton = 3;

        protected override void Awake()
        {
            base.Awake();

            repeatToggle.onValueChanged.AddListener(value =>
            {
                var stage = Game.Game.instance.Stage;
                stage.IsRepeatStage = value;
                if (value)
                {
                    stage.IsExitReserved = false;
                }
            });

            exitToggle.onValueChanged.AddListener(value =>
            {
                var stage = Game.Game.instance.Stage;
                stage.IsExitReserved = value;
                if (value)
                {
                    OneLineSystem.Push(
                        MailType.System,
                        L10nManager.Localize("UI_BATTLE_EXIT_RESERVATION_TITLE"),
                        NotificationCell.NotificationType.Information);
                    stage.IsRepeatStage = false;
                }
            });

            Game.Event.OnGetItem.AddListener(_ =>
            {
                var headerMenu = Find<HeaderMenuStatic>();
                if (!headerMenu)
                {
                    throw new WidgetNotFoundException<HeaderMenuStatic>();
                }

                var target = headerMenu.GetToggle(HeaderMenuStatic.ToggleType.AvatarInfo);
                VFXController.instance.CreateAndChase<DropItemInventoryVFX>(target, Vector3.zero);
            });
            CloseWidget = null;
        }

        public override void Close(bool ignoreCloseAnimation = false)
        {
            guidedQuest.Hide(ignoreCloseAnimation);
            enemyPlayerStatus.Close(ignoreCloseAnimation);
            Find<HeaderMenuStatic>().Close();
            base.Close(ignoreCloseAnimation);
        }

        protected override void OnCompleteOfCloseAnimationInternal()
        {
            base.OnCompleteOfCloseAnimationInternal();
            stageProgressBar.Close();
        }

        public void ShowInArena(bool ignoreShowAnimation = false)
        {
            Find<HeaderMenuStatic>().Close(true);
            stageText.gameObject.SetActive(false);
            comboText.Close();
            stageProgressBar.Close();
            guidedQuest.Hide(true);
            repeatToggle.gameObject.SetActive(false);
            exitToggle.gameObject.SetActive(false);
            helpButton.gameObject.SetActive(false);
            boostEffectObject.SetActive(false);
            base.Show(ignoreShowAnimation);
        }

        public void Show(int stageId, bool isRepeat, bool isExitReserved, bool isTutorial, int boostCost)
        {
            if (isTutorial)
            {
                ShowForTutorial(false, stageId);
                return;
            }

            guidedQuest.Hide(true);
            base.Show();
            guidedQuest.Show(States.Instance.CurrentAvatarState, () =>
            {
                guidedQuest.SetWorldQuestToInProgress(stageId);
            });

            stageText.text = $"STAGE {StageInformation.GetStageIdString(stageId, true)}";
            stageText.gameObject.SetActive(true);
            stageProgressBar.Show();
            bossStatus.Close();
            enemyPlayerStatus.Close();
            comboText.Close();

            exitToggle.isOn = isExitReserved;
            //repeatToggle.isOn = isExitReserved ? false : isRepeat;
            helpButton.gameObject.SetActive(true);
            //repeatToggle.gameObject.SetActive(true);
            var cost = Game.Game.instance
                .TableSheets.StageSheet.Values.First(i => i.Id == stageId).CostAP;
            boostEffectObject.SetActive(boostCost > cost);
            exitToggle.gameObject.SetActive(true);
            exitToggle.isOn = boostCost > cost;
            boostCountText.text = $"<sprite name=UI_main_icon_star><size=75%>{boostCost}</size>";
        }

        public void ClearStage(int stageId, System.Action<bool> onComplete)
        {
            guidedQuest.ClearWorldQuest(stageId, cleared =>
            {
                if (!cleared)
                {
                    onComplete(false);
                    return;
                }

                guidedQuest.UpdateList(
                    States.Instance.CurrentAvatarState,
                    () => onComplete(true));
            });
        }

        public void ShowComboText(bool attacked)
        {
            comboText.StopAllCoroutines();
            comboText.Show(attacked);
        }

        #region tutorial
        public void ShowForTutorial(bool isPrologue, int stageId = 0)
        {
            if (isPrologue)
            {
                stageProgressBar.Close();
            }
            else
            {
                stageText.text = $"STAGE {StageInformation.GetStageIdString(stageId, true)}";
                stageText.gameObject.SetActive(true);
                stageProgressBar.Show();
            }

            guidedQuest.gameObject.SetActive(false);
            bossStatus.gameObject.SetActive(false);
            repeatToggle.gameObject.SetActive(false);
            helpButton.gameObject.SetActive(false);
            bossStatus.gameObject.SetActive(false);
            comboText.gameObject.SetActive(false);
            enemyPlayerStatus.gameObject.SetActive(false);
            exitToggle.gameObject.SetActive(false);
            comboText.comboMax = 5;
            gameObject.SetActive(true);
            Find<HeaderMenuStatic>().Close(true);
        }
        #endregion
    }
}
