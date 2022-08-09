using UnityEngine;
using UnityEngine.UI;

namespace Nekoyume.UI
{
    public class ChatPopup : PopupWidget
    {
        [SerializeField] private Button confirm;
        [SerializeField] private Button cancel;

        public override void Initialize()
        {
            confirm.onClick.AddListener(() =>
            {
                Application.OpenURL(GameConfig.DiscordLink);
                HelpTooltip.HelpMe(100012, true);
                Close(true);
            });

            cancel.onClick.AddListener(() =>
            {
                Close(true);
            });

            base.Initialize();
        }

        protected override void OnEnable()
        {
            SubmitWidget = () =>
            {
                Application.OpenURL(GameConfig.DiscordLink);
                HelpTooltip.HelpMe(100012, true);
                Close(true);
            };

            CloseWidget = () => Close(true);
            base.OnEnable();
        }


        public override void Show(bool ignoreShowAnimation = false)
        {
            base.Show(true);
        }
    }
}
