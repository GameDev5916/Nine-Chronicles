using Nekoyume.EnumType;
using Nekoyume.Game.Controller;

namespace Nekoyume.UI
{
    public class PopupWidget : Widget
    {
        public override WidgetType WidgetType => WidgetType.Popup;
        public override CloseKeyType CloseKeyType => CloseKeyType.Escape;

        public override void Show(bool ignoreShowAnimation = false)
        {
            base.Show(ignoreShowAnimation);
            PlayPopupSound();
        }

        protected virtual void PlayPopupSound() => AudioController.PlayPopup();
    }
}
