using Libplanet;
using Nekoyume.L10n;
using Nekoyume.Model.State;
using Nekoyume.State;
using Nekoyume.UI.Model;
using Nekoyume.UI.Module;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Nekoyume.UI.Scroller
{
    public class RankCellPanel : MonoBehaviour
    {
        [SerializeField]
        private Image rankImage = null;

        [SerializeField]
        private TextMeshProUGUI rankText = null;

        [SerializeField]
        private DetailedCharacterView characterView = null;

        [SerializeField]
        private TextMeshProUGUI nicknameText = null;

        [SerializeField]
        private TextMeshProUGUI addressText = null;

        [SerializeField]
        private TextMeshProUGUI firstElementCpText = null;

        [SerializeField]
        private TextMeshProUGUI firstElementText = null;

        [SerializeField]
        private TextMeshProUGUI secondElementText = null;

        [SerializeField]
        private TextMeshProUGUI secondElementEquipmentNameText = null;

        [SerializeField]
        private Sprite firstPlaceSprite = null;

        [SerializeField]
        private Sprite secondPlaceSprite = null;

        [SerializeField]
        private Sprite thirdPlaceSprite = null;

        [SerializeField]
        private int addressStringCount = 6;

        private RankingModel _model = null;

        private void Awake()
        {
            characterView.OnClickCharacterIcon
                .Subscribe(async avatarState =>
                {
                    var loadingScreen = Widget.Find<GrayLoadingScreen>();
                    loadingScreen.Show("UI_LOADING_STATES", true);
                    if (avatarState is null)
                    {
                        // remove "0x"
                        var address = new Address(_model.AvatarAddress.Substring(2));
                        var (exist, state) = await States.TryGetAvatarStateAsync(address);
                        avatarState = exist ? state : null;
                    }
                    Widget.Find<FriendInfoPopup>().Show(avatarState);
                    loadingScreen.Close();
                })
                .AddTo(gameObject);
        }

        public void SetData<T>(T rankingInfo) where T : RankingModel
        {
            _model = rankingInfo;
            nicknameText.text = rankingInfo.Name;
            nicknameText.gameObject.SetActive(true);
            addressText.text = rankingInfo.AvatarAddress
                .Remove(addressStringCount);

            UpdateRank(rankingInfo.Rank);
            characterView.SetByArmorId(
                rankingInfo.ArmorId,
                rankingInfo.TitleId,
                rankingInfo.AvatarLevel);
            gameObject.SetActive(true);

            switch (rankingInfo)
            {
                case AbilityRankingModel abilityInfo:
                    firstElementCpText.text = abilityInfo.Cp.ToString();
                    secondElementText.text = rankingInfo.AvatarLevel.ToString();

                    firstElementText.gameObject.SetActive(false);
                    firstElementCpText.gameObject.SetActive(true);
                    secondElementText.gameObject.SetActive(true);
                    secondElementEquipmentNameText.gameObject.SetActive(false);
                    break;
                case StageRankingModel stageInfo:
                    firstElementText.text = stageInfo.ClearedStageId.ToString();

                    firstElementText.gameObject.SetActive(true);
                    firstElementCpText.gameObject.SetActive(false);
                    secondElementText.gameObject.SetActive(false);
                    secondElementEquipmentNameText.gameObject.SetActive(false);
                    break;
                case CraftRankingModel craftInfo:
                    firstElementText.text = craftInfo.CraftCount.ToString();

                    firstElementText.gameObject.SetActive(true);
                    firstElementCpText.gameObject.SetActive(false);
                    secondElementText.gameObject.SetActive(false);
                    secondElementEquipmentNameText.gameObject.SetActive(false);
                    break;
                case EquipmentRankingModel equipmentInfo:
                    firstElementCpText.text = equipmentInfo.Cp.ToString();

                    if (Game.Game.instance.TableSheets.EquipmentItemSheet.TryGetValue(equipmentInfo.EquipmentId, out var row))
                    {
                        secondElementEquipmentNameText.text =
                            row.GetLocalizedName(equipmentInfo.Level);
                    }
                    else
                    {
                        secondElementEquipmentNameText.text = $"!{equipmentInfo.EquipmentId}! +{equipmentInfo.Level}";
                    }

                    firstElementText.gameObject.SetActive(false);
                    firstElementCpText.gameObject.SetActive(true);
                    secondElementText.gameObject.SetActive(false);
                    secondElementEquipmentNameText.gameObject.SetActive(true);
                    break;
            }
        }

        private void UpdateRank(int? rank)
        {
            switch (rank)
            {
                case null:
                case 0:
                    rankImage.gameObject.SetActive(false);
                    rankText.gameObject.SetActive(true);
                    rankText.text = "-";
                    break;
                case 1:
                    rankImage.gameObject.SetActive(true);
                    rankText.gameObject.SetActive(false);
                    rankImage.sprite = firstPlaceSprite;
                    break;
                case 2:
                    rankImage.gameObject.SetActive(true);
                    rankText.gameObject.SetActive(false);
                    rankImage.sprite = secondPlaceSprite;
                    break;
                case 3:
                    rankImage.gameObject.SetActive(true);
                    rankText.gameObject.SetActive(false);
                    rankImage.sprite = thirdPlaceSprite;
                    break;
                default:
                    rankImage.gameObject.SetActive(false);
                    rankText.gameObject.SetActive(true);
                    rankText.text = rank.ToString();
                    break;
            }
        }

        public void SetEmpty(AvatarState avatarState)
        {
            rankText.text = "-";
            characterView.SetByAvatarState(avatarState);
            nicknameText.text = avatarState.name;
            addressText.text = avatarState.address.ToString()
                .Remove(addressStringCount);

            firstElementText.text = "-";
            firstElementText.gameObject.SetActive(true);
            firstElementCpText.gameObject.SetActive(false);
            secondElementText.gameObject.SetActive(false);
            secondElementEquipmentNameText.gameObject.SetActive(false);
        }
    }
}
