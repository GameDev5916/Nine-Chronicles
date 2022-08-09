//#define TEST_LOG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using mixpanel;
using Nekoyume.Battle;
using Nekoyume.BlockChain;
using Nekoyume.Game.Character;
using Nekoyume.Game.Controller;
using Nekoyume.Game.Entrance;
using Nekoyume.Game.Factory;
using Nekoyume.Game.Trigger;
using Nekoyume.Game.Util;
using Nekoyume.Game.VFX;
using Nekoyume.Game.VFX.Skill;
using Nekoyume.Helper;
using Nekoyume.L10n;
using Nekoyume.Model;
using Nekoyume.Model.BattleStatus;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.State;
using Nekoyume.UI;
using Nekoyume.UI.Model;
using Nekoyume.UI.Module;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using CharacterBase = Nekoyume.Model.CharacterBase;
using Enemy = Nekoyume.Model.Enemy;
using EnemyPlayer = Nekoyume.Model.EnemyPlayer;
using Player = Nekoyume.Game.Character.Player;
using Random = UnityEngine.Random;

namespace Nekoyume.Game
{
    using UniRx;

    public class Stage : MonoBehaviour, IStage
    {
        public const float StageStartPosition = -1.2f;
        private const float SkillDelay = 0.1f;
        public ObjectPool objectPool;
        public NPCFactory npcFactory;
        public DropItemFactory dropItemFactory;

        public MonsterSpawner spawner;

        private GameObject _background;

        // dummy for stage background moving.
        public GameObject dummy;
        public ParticleSystem defaultBGVFX;
        public ParticleSystem bosswaveBGVFX;

        public int worldId;
        public int stageId;
        public int waveCount;
        public bool newlyClearedStage;
        public int waveNumber;
        public int waveTurn;

        public int foodCount;
        public string zone;
        public Animator roomAnimator { get; private set; }

        private Camera _camera;
        private BattleLog _battleLog;
        private BattleResultPopup.Model _battleResultModel;
        private bool _rankingBattle;
        private Coroutine _battleCoroutine;
        private Player _stageRunningPlayer;
        private Vector3 _playerPosition;
        private Coroutine _positionCheckCoroutine;
        private List<int> prevFood;

        public Player SelectedPlayer { get; set; }
        public List<GameObject> ReleaseWhiteList { get; private set; } = new List<GameObject>();
        public SkillController SkillController { get; private set; }
        public BuffController BuffController { get; private set; }
        public TutorialController TutorialController { get; private set; }
        public Enemy Boss { get; private set; }
        public AvatarState AvatarState { get; set; }
        public bool IsInStage { get; set; }
        public bool IsShowHud { get; set; }
        public bool IsExitReserved { get; set; }
        public bool IsRepeatStage { get; set; }
        public bool IsAvatarStateUpdatedAfterBattle { get; set; }
        public int PlayCount { get; set; }

        public Vector3 SelectPositionBegin(int index) =>
            new Vector3(-2.15f + index * 2.22f, -1.79f, 0.0f);

        public Vector3 SelectPositionEnd(int index) =>
            new Vector3(-2.15f + index * 2.22f, -0.25f, 0.0f);

        public bool showLoadingScreen;

        #region Events

        private readonly ISubject<Stage> _onEnterToStageEnd = new Subject<Stage>();
        public IObservable<Stage> onEnterToStageEnd => _onEnterToStageEnd;

        public readonly ISubject<Stage> OnRoomEnterEnd = new Subject<Stage>();

        #endregion

        protected void Awake()
        {
            _camera = ActionCamera.instance.Cam;
            if (_camera is null)
            {
                throw new NullReferenceException("`Camera.main` can't be null.");
            }

            if (dummy is null)
            {
                throw new NullReferenceException("`Dummy` can't be null.");
            }

            Event.OnNestEnter.AddListener(OnNestEnter);
            Event.OnLoginDetail.AddListener(OnLoginDetail);
            Event.OnRoomEnter.AddListener(OnRoomEnter);
            Event.OnStageStart.AddListener(OnStageStart);
            Event.OnRankingBattleStart.AddListener(OnRankingBattleStart);
        }

        public void Initialize()
        {
            objectPool.Initialize();
            dropItemFactory.Initialize();
            SkillController = new SkillController(objectPool);
            BuffController = new BuffController(objectPool);
            TutorialController = new TutorialController(MainCanvas.instance.Widgets);
        }

        private void OnStageStart(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(OnStageStart)}() enter");
#endif
            _rankingBattle = false;
            if (_battleLog is null)
            {
                if (!(_battleCoroutine is null))
                {
                    StopCoroutine(_battleCoroutine);
                    _battleCoroutine = null;
                    objectPool.ReleaseAll();
                }
                _battleLog = log;
                PlayStage(_battleLog);
            }
            else
            {
                Debug.Log("Skip incoming battle. Battle is already simulating.");
            }
        }

        private void OnRankingBattleStart(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(OnRankingBattleStart)}() enter");
#endif
            _rankingBattle = true;
            if (_battleLog is null)
            {
                if (!(_battleCoroutine is null))
                {
                    StopCoroutine(_battleCoroutine);
                    _battleCoroutine = null;
                    objectPool.ReleaseAll();
                }
                _battleLog = log;
                PlayRankingBattle(_battleLog);
            }
            else
            {
                Debug.Log("Skip incoming battle. Battle is already simulating.");
            }
        }

        private void OnNestEnter()
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(OnNestEnter)}() enter");
#endif
            gameObject.AddComponent<NestEntering>();
        }

        private void OnLoginDetail(int index)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(OnLoginDetail)}({index}) enter");
#endif
            DOTween.KillAll();
            var players = Widget.Find<Login>().players;
            for (var i = 0; i < players.Count; ++i)
            {
                var player = players[i];
                var playerObject = player.gameObject;
                var anim = player.Animator;
                if (index == i)
                {
                    var moveTo = new Vector3(-0.05f, -0.5f);
                    playerObject.transform.DOScale(1.1f, 2.0f).SetDelay(0.2f);
                    playerObject.transform.DOMove(moveTo, 1.3f).SetDelay(0.2f);
                    var seqPos = new Vector3(
                        moveTo.x,
                        moveTo.y - Random.Range(0.05f, 0.1f),
                        0.0f);
                    var seq = DOTween.Sequence();
                    seq.Append(playerObject.transform.DOMove(
                        seqPos,
                        Random.Range(4.0f, 5.0f)));
                    seq.Append(playerObject.transform.DOMove(
                        moveTo,
                        Random.Range(4.0f, 5.0f)));
                    seq.Play().SetDelay(2.6f).SetLoops(-1);
                    if (!ReferenceEquals(anim, null) && !anim.Target.activeSelf)
                    {
                        anim.Target.SetActive(true);
                        var skeleton =
                            anim.Target.GetComponentInChildren<SkeletonAnimation>().skeleton;
                        skeleton.A = 0.0f;
                        DOTween.To(() => skeleton.A, x => skeleton.A = x, 1.0f, 1.0f);
                        player.SpineController.Appear();
                    }

                    SelectedPlayer = players[i];
                }
                else
                {
                    playerObject.transform.DOScale(0.9f, 1.0f);
                    playerObject.transform.DOMoveY(-3.6f, 2.0f);

                    if (!ReferenceEquals(anim, null) && anim.Target.activeSelf)
                    {
                        anim.Target.SetActive(true);
                        player.SpineController.Disappear();
                    }
                }
            }
        }

        private void OnRoomEnter(bool showScreen)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(OnRoomEnter)}() enter");
#endif
            showLoadingScreen = showScreen;
            gameObject.AddComponent<RoomEntering>();
            IsInStage = false;
        }

        // todo: 배경 캐싱.
        public void LoadBackground(string prefabName, float fadeTime = 0.0f)
        {
            if (_background)
            {
                if (_background.name.Equals(prefabName))
                    return;

                if (fadeTime > 0.0f)
                {
                    var sprites = _background.GetComponentsInChildren<SpriteRenderer>();
                    foreach (var sprite in sprites)
                    {
                        sprite.sortingOrder += 1;
                        sprite.DOFade(0.0f, fadeTime);
                    }

                    var particles = _background.GetComponentsInChildren<ParticleSystem>();
                    foreach (var particle in particles)
                    {
                        particle.Stop();
                    }
                }

                DestroyBackground(fadeTime);
            }

            var path = $"Prefab/Background/{prefabName}";
            var prefab = Resources.Load<GameObject>(path);
            if (!prefab)
                throw new FailedToLoadResourceException<GameObject>(path);

            _background = Instantiate(prefab, transform);
            _background.name = prefabName;

            foreach (Transform child in _background.transform)
            {
                var childName = child.name;
                if (!childName.StartsWith("bgvfx"))
                    continue;

                var num = childName.Substring(childName.Length - 2);
                switch (num)
                {
                    case "01":
                        defaultBGVFX = child.GetComponent<ParticleSystem>();
                        break;
                    case "02":
                        bosswaveBGVFX = child.GetComponent<ParticleSystem>();
                        break;
                }
            }
        }

        public void DestroyBackground(float fadeTime = 0f)
        {
            Destroy(_background, fadeTime);
            _background = null;
        }

        public void PlayStage(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(PlayStage)}() enter");
#endif
            if (log?.Count > 0)
            {
                _battleCoroutine = StartCoroutine(CoPlayStage(log));
            }
        }

        private void PlayRankingBattle(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(PlayRankingBattle)}() enter");
#endif
            if (log?.Count > 0)
            {
                _battleCoroutine = StartCoroutine(CoPlayRankingBattle(log));
            }
        }

        private IEnumerator CoPlayStage(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoPlayStage)}() enter");
#endif
            var avatarState = States.Instance.CurrentAvatarState;
            prevFood = avatarState.inventory.Items
                .Select(i => i.item)
                .OfType<Consumable>()
                .Where(s => s.ItemSubType == ItemSubType.Food)
                .Select(r => r.Id)
                .ToList();

            IsInStage = true;
            yield return StartCoroutine(CoStageEnter(log));
            HelpTooltip.HelpMe(100005, true);
            foreach (var e in log)
            {
                yield return StartCoroutine(e.CoExecute(this));
            }

            yield return StartCoroutine(CoStageEnd(log));
            ClearBattle();
        }

        private IEnumerator CoPlayRankingBattle(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoPlayRankingBattle)}() enter");
#endif
            IsInStage = true;
            yield return StartCoroutine(CoRankingBattleEnter(log));
            Widget.Find<ArenaBattleLoadingScreen>().Close();
            _positionCheckCoroutine = StartCoroutine(CheckPosition(log));
            foreach (var e in log)
            {
                yield return StartCoroutine(e.CoExecute(this));
            }
            StopCoroutine(_positionCheckCoroutine);
            _positionCheckCoroutine = null;
            yield return StartCoroutine(CoRankingBattleEnd(log));
            ClearBattle();
        }

        private IEnumerator CheckPosition(BattleLog log)
        {
            var player = GetPlayer();
            while (player.isActiveAndEnabled)
            {
                if (player.transform.localPosition.x >= 16f)
                {
                    _positionCheckCoroutine = null;

                    if (log.FirstOrDefault(e => e is GetReward) is GetReward getReward)
                    {
                        var rewards = getReward.Rewards;
                        foreach (var item in rewards)
                        {
                            var countableItem = new CountableItem(item, 1);
                            _battleResultModel.AddReward(countableItem);
                        }
                    }

                    yield return StartCoroutine(CoRankingBattleEnd(log, true));
                    ClearBattle();
                    StopAllCoroutines();
                }

                yield return new WaitForSeconds(1f);
            }
        }

        public void ClearBattle()
        {
            _battleLog = null;
            if (!(_battleCoroutine is null))
            {
                StopCoroutine(_battleCoroutine);
                _battleCoroutine = null;
            }
        }

        private static IEnumerator CoGuidedQuest(int stageIdToClear)
        {
            var done = false;
            var battle = Widget.Find<UI.Battle>();
            battle.ClearStage(stageIdToClear, cleared => done = true);
            yield return new WaitUntil(() => done);
        }

        private static IEnumerator CoUnlockRecipe(int stageIdToFirstClear)
        {
            var questResult = Widget.Find<CelebratesPopup>();
            var rows = Game.instance.TableSheets.EquipmentItemRecipeSheet.OrderedList
                .Where(row => row.UnlockStage == stageIdToFirstClear)
                .Distinct()
                .ToList();
            foreach (var row in rows)
            {
                questResult.Show(row);
                yield return new WaitWhile(() => questResult.IsActive());
            }
        }

        private IEnumerator CoStageEnter(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoStageEnter)}() enter");
#endif
            worldId = log.worldId;
            stageId = log.stageId;
            waveCount = log.waveCount;
            newlyClearedStage = log.newlyCleared;
            if (!Game.instance.TableSheets.StageSheet.TryGetValue(stageId, out var data))
                yield break;

            _battleResultModel = new BattleResultPopup.Model();

            zone = data.Background;
            LoadBackground(zone, 3.0f);
            PlayBGVFX(false);
            RunPlayer();
            ReleaseWhiteList.Clear();
            ReleaseWhiteList.Add(_stageRunningPlayer.gameObject);

            Game.instance.TableSheets.StageSheet.TryGetValue(stageId, out var stageData);
            Widget.Find<UI.Battle>().StageProgressBar.Initialize(true);
            Widget.Find<BattleResultPopup>().StageProgressBar.Initialize(false);
            var title = Widget.Find<StageTitle>();
            title.Show(stageId);
            IsShowHud = false;
            yield return new WaitForSeconds(StageConfig.instance.stageEnterDelay);

            yield return StartCoroutine(title.CoClose());

            AudioController.instance.PlayMusic(data.BGM);
            IsShowHud = true;
        }

        private IEnumerator CoRankingBattleEnter(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoRankingBattleEnter)}() enter");
#endif
            waveCount = log.waveCount;
            waveTurn = 1;
            stageId = log.stageId;
            if (!Game.instance.TableSheets.StageSheet.TryGetValue(stageId, out var data))
                yield break;

            _battleResultModel = new BattleResultPopup.Model();

            zone = data.Background;
            LoadBackground(zone, 3.0f);
            PlayBGVFX(false);
            RunPlayer(new Vector2(-15f, -1.2f), false);

            yield return new WaitForSeconds(2.0f);

            AudioController.instance.PlayMusic(AudioController.MusicCode.PVPBattle);
        }

        private IEnumerator CoStageEnd(BattleLog log)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoStageEnd)}() enter");
#endif
            IsAvatarStateUpdatedAfterBattle = false;
            // NOTE ActionRenderHandler.Instance.Pending should be false before _onEnterToStageEnd.OnNext() invoked.
            ActionRenderHandler.Instance.Pending = false;
            _onEnterToStageEnd.OnNext(this);
            yield return new WaitUntil(() => IsAvatarStateUpdatedAfterBattle);
            var avatarState = States.Instance.CurrentAvatarState;

            _battleResultModel.ClearedWaveNumber = log.clearedWaveNumber;
            var characters = GetComponentsInChildren<Character.CharacterBase>();
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            yield return new WaitForSeconds(1f);
            Boss = null;
            Widget.Find<UI.Battle>().BossStatus.Close();
            var isClear = log.IsClear;
            if (isClear)
            {
                yield return StartCoroutine(CoGuidedQuest(log.stageId));
                yield return new WaitForSeconds(1f);
            }
            else
            {
                var enemies = GetComponentsInChildren<Character.Enemy>();
                if (enemies.Any())
                {
                    foreach (var enemy in enemies)
                    {
                        if (enemy.isActiveAndEnabled)
                        {
                            enemy.Animator.Win();
                        }
                    }
                    yield return new WaitForSeconds(1f);
                }
            }

            Widget.Find<UI.Battle>().Close();

            if (newlyClearedStage)
            {
                yield return StartCoroutine(CoUnlockMenu());
                yield return new WaitForSeconds(0.75f);
                yield return StartCoroutine(CoUnlockRecipe(stageId));
                yield return new WaitForSeconds(1f);
            }

            IsShowHud = false;
            if (log.result == BattleLog.Result.Win)
            {
                _stageRunningPlayer.DisableHUD();
                _stageRunningPlayer.Animator.Win(log.clearedWaveNumber);
                _stageRunningPlayer.ShowSpeech("PLAYER_WIN");
                yield return new WaitForSeconds(2.2f);
                objectPool.ReleaseExcept(ReleaseWhiteList);
                if (isClear)
                {
                    StartCoroutine(CoSlideBg());
                }
            }
            else
            {
                if (log.result == BattleLog.Result.TimeOver)
                {
                    _stageRunningPlayer.Animator.TurnOver();
                    yield return new WaitForSeconds(2f);
                }
                ReleaseWhiteList.Remove(_stageRunningPlayer.gameObject);
                objectPool.ReleaseExcept(ReleaseWhiteList);
            }

            _battleResultModel.ActionPoint = avatarState.actionPoint;
            _battleResultModel.State = log.result;
            Game.instance.TableSheets.WorldSheet.TryGetValue(log.worldId, out var world);
            _battleResultModel.WorldName = world?.GetLocalizedName();
            _battleResultModel.WorldID = log.worldId;
            _battleResultModel.StageID = log.stageId;
            avatarState.worldInformation.TryGetLastClearedStageId(out var lasStageId);
            _battleResultModel.LastClearedStageId = lasStageId;
            _battleResultModel.IsClear = log.IsClear;
            var succeedToGetWorldRow =
                Game.instance.TableSheets.WorldSheet.TryGetValue(worldId, out var worldRow);
            if (succeedToGetWorldRow)
            {
                _battleResultModel.IsEndStage = stageId == worldRow.StageEnd;
            }

            if (IsExitReserved)
            {
                _battleResultModel.NextState = BattleResultPopup.NextState.GoToMain;
                _battleResultModel.ActionPointNotEnough = false;
            }
            else
            {
                var apNotEnough = true;
                if (Game.instance.TableSheets.StageSheet.TryGetValue(stageId, out var stageRow))
                {
                    apNotEnough = avatarState.actionPoint < stageRow.CostAP;
                }

                _battleResultModel.ActionPointNotEnough = apNotEnough;
                if (apNotEnough)
                {
                    _battleResultModel.NextState = BattleResultPopup.NextState.GoToMain;
                }
                else
                {
                    if (isClear)
                    {
                        _battleResultModel.NextState = IsRepeatStage
                            ? BattleResultPopup.NextState.RepeatStage
                            : BattleResultPopup.NextState.NextStage;

                        if (succeedToGetWorldRow)
                        {
                            if (stageId == worldRow.StageEnd)
                            {
                                _battleResultModel.NextState = IsRepeatStage ?
                                    BattleResultPopup.NextState.RepeatStage :
                                    BattleResultPopup.NextState.GoToMain;
                            }
                        }
                    }
                    else
                    {
                        _battleResultModel.NextState = IsRepeatStage ?
                            BattleResultPopup.NextState.RepeatStage :
                            BattleResultPopup.NextState.GoToMain;
                    }
                }
            }

            Widget.Find<BattleResultPopup>().Show(_battleResultModel, PlayCount > 1);

            yield return null;

            var characterSheet = Game.instance.TableSheets.CharacterSheet;
            var costumeStatSheet = Game.instance.TableSheets.CostumeStatSheet;
            var cp = CPHelper.GetCPV2(States.Instance.CurrentAvatarState, characterSheet, costumeStatSheet);
            var props = new Value
            {
                ["StageId"] = log.stageId,
                ["ClearedWave"] = log.clearedWaveNumber,
                ["Repeat"] = IsRepeatStage,
                ["CP"] = cp,
                ["FoodCount"] = foodCount
            };
            Analyzer.Instance.Track("Unity/Stage End", props);
        }

        private IEnumerator CoSlideBg()
        {
            RunPlayer();
            while (Widget.Find<BattleResultPopup>().IsActive())
            {
                yield return new WaitForEndOfFrame();
            }
        }

        private IEnumerator CoRankingBattleEnd(BattleLog log, bool forceQuit = false)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoRankingBattleEnd)}() enter");
#endif
            IsAvatarStateUpdatedAfterBattle = false;

            // NOTE ActionRenderHandler.Instance.Pending should be false before _onEnterToStageEnd.OnNext() invoked.
            ActionRenderHandler.Instance.Pending = false;
            _onEnterToStageEnd.OnNext(this);
            yield return new WaitUntil(() => IsAvatarStateUpdatedAfterBattle);

            var characters = GetComponentsInChildren<Character.CharacterBase>();

            if (!forceQuit)
            {
                yield return new WaitWhile(() =>
                    characters.Any(i => i.actions.Any()));
            }

            Boss = null;
            var playerCharacter = log.result == BattleLog.Result.Win
                ? GetPlayer()
                : GetComponentInChildren<Character.EnemyPlayer>();

            yield return new WaitForSeconds(0.75f);
            playerCharacter.Animator.Win();
            playerCharacter.ShowSpeech("PLAYER_WIN");
            Widget.Find<UI.Battle>().Close();
            Widget.Find<Status>().Close();

            Widget.Find<RankingBattleResultPopup>().Show(log, _battleResultModel.Rewards);
            yield return null;
        }

        public IEnumerator CoSpawnPlayer(Model.Player character)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoSpawnPlayer)}() enter");
#endif
            var playerCharacter = RunPlayer(false);
            playerCharacter.Set(character, true);
            playerCharacter.ShowSpeech("PLAYER_INIT");
            var player = playerCharacter.gameObject;
            player.SetActive(true);

            var status = Widget.Find<Status>();
            status.UpdatePlayer(playerCharacter);
            status.Show();
            status.ShowBattleStatus();

            var battle = Widget.Find<UI.Battle>();
            if (_rankingBattle)
            {
                battle.ShowInArena();
            }
            else
            {
                var isTutorial = false;
                if (States.Instance.CurrentAvatarState.worldInformation
                    .TryGetUnlockedWorldByStageClearedBlockIndex(out var worldInfo))
                {
                    if (worldInfo.StageClearedId < UI.Battle.RequiredStageForExitButton)
                    {
                        Widget.Find<HeaderMenuStatic>().Close(true);
                        isTutorial = true;
                    }
                    else
                    {
                        Widget.Find<HeaderMenuStatic>().Show();
                    }
                }
                else
                {
                    Widget.Find<HeaderMenuStatic>().Close(true);
                    isTutorial = true;
                }

                battle.Show(stageId, IsRepeatStage, IsExitReserved, isTutorial, PlayCount * Game
                    .instance
                    .TableSheets.StageSheet.Values.First(i => i.Id == stageId).CostAP);
                var stageSheet = Game.instance.TableSheets.StageSheet;
                if (stageSheet.TryGetValue(stageId, out var row))
                {
                    status.ShowBattleTimer(row.TurnLimit);
                }
            }

            if (!(AvatarState is null) && !ActionRenderHandler.Instance.Pending)
            {
                ActionRenderHandler.Instance.UpdateCurrentAvatarStateAsync(AvatarState);
            }

            yield return null;
        }

        public IEnumerator CoSpawnEnemyPlayer(EnemyPlayer character)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoSpawnEnemyPlayer)}() enter");
#endif
            var battle = Widget.Find<UI.Battle>();
            battle.BossStatus.Close();
            battle.EnemyPlayerStatus.Show();
            battle.EnemyPlayerStatus.SetHp(character.CurrentHP, character.HP);

            var sprite =
                SpriteHelper.GetItemIcon(character.armor?.Id ?? GameConfig.DefaultAvatarArmorId);
            battle.EnemyPlayerStatus.SetProfile(character.Level, character.NameWithHash, sprite);
            yield return StartCoroutine(spawner.CoSetData(character, new Vector3(8f, -1.2f)));
        }
        #region Skill

        public IEnumerator CoNormalAttack(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoNormalAttack);
                character.actions.Add(actionParams);
                yield return null;
            }
        }

        public IEnumerator CoBlowAttack(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoBlowAttack);
                character.actions.Add(actionParams);
                yield return null;
            }
        }

        public IEnumerator CoDoubleAttack(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoDoubleAttack);
                character.actions.Add(actionParams);

                yield return null;
            }
        }

        public IEnumerator CoAreaAttack(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoAreaAttack);
                character.actions.Add(actionParams);

                yield return null;
            }
        }

        public IEnumerator CoHeal(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoHeal);
                character.actions.Add(actionParams);
                yield return null;
            }
        }

        public IEnumerator CoBuff(
            CharacterBase caster,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                var actionParams = new ActionParams(character, skillInfos, buffInfos, character.CoBuff);
                character.actions.Add(actionParams);
                yield return null;
            }
        }

        public IEnumerator CoSkill(ActionParams param)
        {
            yield return StartCoroutine(CoSkill(param.character, param.skillInfos, param.buffInfos, param.func));
        }

        private IEnumerator CoSkill(
            Character.CharacterBase character,
            IEnumerable<Skill.SkillInfo> skillInfos,
            IEnumerable<Skill.SkillInfo> buffInfos,
            Func<IReadOnlyList<Skill.SkillInfo>, IEnumerator> func)
        {
            if (!character)
                throw new ArgumentNullException(nameof(character));

            var infos = skillInfos.ToList();
            var infosFirstWaveTurn = infos.First().WaveTurn;
            var time = Time.time;
            yield return new WaitUntil(() => Time.time - time > 5f ||  waveTurn == infosFirstWaveTurn);
            yield return StartCoroutine(CoBeforeSkill(character));

            yield return StartCoroutine(func(infos));

            yield return StartCoroutine(CoAfterSkill(character, buffInfos));
        }

        #endregion

        public IEnumerator CoDropBox(List<ItemBase> items)
        {
            var prevEnemies = GetComponentsInChildren<Character.Enemy>();
            yield return new WaitWhile(() => prevEnemies.Any(enemy => enemy.isActiveAndEnabled));

            var isHeaderMenuShown = Widget.Find<HeaderMenuStatic>().IsActive();
            if (isHeaderMenuShown && items.Count > 0)
            {
                var player = GetPlayer();
                var position = player.transform.position;
                position.x += 1.0f;
                yield return StartCoroutine(dropItemFactory.CoCreate(items, position));
            }

            yield return null;
        }

        private IEnumerator CoBeforeSkill(Character.CharacterBase character)
        {
            if (!character)
                throw new ArgumentNullException(nameof(character));

            var enemy = GetComponentsInChildren<Character.CharacterBase>()
                .Where(c => c.gameObject.CompareTag(character.TargetTag) && c.IsAlive)
                .OrderBy(c => c.transform.position.x).FirstOrDefault();
            if (!enemy || character.TargetInAttackRange(enemy))
                yield break;

            character.StartRun();
            var time = Time.time;
            yield return new WaitUntil(() =>
                Time.time - time > 2f || character.TargetInAttackRange(enemy));
        }

        private IEnumerator CoAfterSkill(
            Character.CharacterBase character,
            IEnumerable<Skill.SkillInfo> buffInfos)
        {
            if (!character)
                throw new ArgumentNullException(nameof(character));

            character.UpdateHpBar();

            if (!(buffInfos is null))
            {
                foreach (var buffInfo in buffInfos)
                {
                    var buffCharacter = GetCharacter(buffInfo.Target);
                    if (!buffCharacter)
                        throw new ArgumentNullException(nameof(buffCharacter));
                    buffCharacter.UpdateHpBar();
                }
            }

            yield return new WaitForSeconds(SkillDelay);
            var enemy = GetComponentsInChildren<Character.CharacterBase>()
                .Where(c => c.gameObject.CompareTag(character.TargetTag) && c.IsAlive)
                .OrderBy(c => c.transform.position.x).FirstOrDefault();
            if (enemy && !character.TargetInAttackRange(enemy))
                character.StartRun();
        }

        public IEnumerator CoRemoveBuffs(CharacterBase caster)
        {
            var character = GetCharacter(caster);
            if (character)
            {
                character.UpdateHpBar();
                if (character.HPBar.HpVFX != null)
                {
                    character.HPBar.HpVFX.Stop();
                }
            }

            yield break;
        }

        public IEnumerator CoGetReward(List<ItemBase> rewards)
        {
            var characters = GetComponentsInChildren<Character.CharacterBase>();
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            foreach (var item in rewards)
            {
                var countableItem = new CountableItem(item, 1);
                _battleResultModel.AddReward(countableItem);
            }

            yield return null;
        }

        #region wave

        public IEnumerator CoSpawnWave(
            int waveNumber,
            int waveTurn,
            List<Enemy> enemies,
            bool hasBoss)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoSpawnWave)}() enter. {nameof(waveTurn)}({waveTurn})");
#endif
            this.waveNumber = waveNumber;
            this.waveTurn = waveTurn;
            var prevEnemies = GetComponentsInChildren<Character.Enemy>();
            yield return new WaitWhile(() => prevEnemies.Any(enemy => enemy.isActiveAndEnabled));
            foreach (var prev in prevEnemies)
            {
                objectPool.Remove<Character.Enemy>(prev.gameObject);
            }

            Event.OnWaveStart.Invoke(enemies.Sum(enemy => enemy.HP));

            var characters = GetComponentsInChildren<Character.CharacterBase>();
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            yield return new WaitForSeconds(StageConfig.instance.spawnWaveDelay);
            Widget.Find<UI.Battle>().BossStatus.Close();
            Widget.Find<UI.Battle>().EnemyPlayerStatus.Close();
            var playerCharacter = GetPlayer();
            RunAndChasePlayer(playerCharacter);

            if (hasBoss)
            {
                yield return new WaitForSeconds(1.5f);
                playerCharacter.ShowSpeech("PLAYER_BOSS_STAGE");
                yield return new WaitForSeconds(1.5f);
                PlayBGVFX(true);
                AudioController.instance.PlayMusic(AudioController.MusicCode.Boss1);
                VFXController.instance.Create<BattleBossTitleVFX>(Vector3.zero);
                StartCoroutine(Widget.Find<Blind>().FadeIn(0.4f, "", 0.2f));
                yield return new WaitForSeconds(2.0f);
                StartCoroutine(Widget.Find<Blind>().FadeOut(0.2f));
                yield return new WaitForSeconds(2.0f);
                var boss = enemies.Last();
                Boss = boss;
                var sprite = SpriteHelper.GetCharacterIcon(boss.RowData.Id);
                var battle = Widget.Find<UI.Battle>();
                battle.BossStatus.Show();
                battle.BossStatus.SetHp(boss.HP, boss.HP);
                battle.BossStatus.SetProfile(
                    boss.Level,
                    L10nManager.LocalizeCharacterName(boss.RowData.Id),
                    sprite);
                playerCharacter.ShowSpeech("PLAYER_BOSS_ENCOUNTER");
            }

            yield return new WaitForEndOfFrame();

            yield return StartCoroutine(spawner.CoSetData(enemies));
        }

        public IEnumerator CoWaveTurnEnd(int turnNumber, int waveTurn)
        {
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoWaveTurnEnd)} enter. {nameof(this.waveTurn)}({this.waveTurn}) [para : waveTurn :{waveTurn}");
#endif
            yield return new WaitWhile(() => SelectedPlayer.actions.Any());
            Event.OnPlayerTurnEnd.Invoke(turnNumber);
            var characters = GetComponentsInChildren<Character.CharacterBase>();
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoWaveTurnEnd)} ing. {nameof(this.waveTurn)}({this.waveTurn}) [para : waveTurn :{waveTurn}");
#endif
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            this.waveTurn = waveTurn;
#if TEST_LOG
            Debug.Log($"[{nameof(Stage)}] {nameof(CoWaveTurnEnd)} exit. {nameof(this.waveTurn)}({this.waveTurn}) [para : waveTurn :{waveTurn}");
#endif
        }

        #endregion

        public IEnumerator CoGetExp(long exp)
        {
            var characters = GetComponentsInChildren<Character.CharacterBase>();
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            _battleResultModel.Exp += exp;
            var player = GetPlayer();
            yield return StartCoroutine(player.CoGetExp(exp));
        }

        public IEnumerator CoDead(CharacterBase model)
        {
            var characters = GetComponentsInChildren<Character.CharacterBase>();
            yield return new WaitWhile(() => characters.Any(i => i.actions.Any()));
            var character = GetCharacter(model);
            _playerPosition = SelectedPlayer.transform.position;
            character.Dead();
        }

        public Player GetPlayer()
        {
            if (SelectedPlayer &&
                SelectedPlayer.gameObject.activeSelf)
            {
                return SelectedPlayer;
            }

            if (SelectedPlayer)
            {
                objectPool.Remove<Model.Player>(SelectedPlayer.gameObject);
            }

            var go = PlayerFactory.Create(States.Instance.CurrentAvatarState);
            SelectedPlayer = go.GetComponent<Player>();

            if (SelectedPlayer is null)
            {
                throw new NotFoundComponentException<Player>();
            }

            return SelectedPlayer;
        }

        public Player GetPlayer(Vector3 position)
        {
            var player = GetPlayer();
            player.transform.position = position;
            return player;
        }

        private Player RunPlayer(bool chasePlayer = true)
        {
            _stageRunningPlayer = GetPlayer();
            var playerTransform = _stageRunningPlayer.transform;
            Vector2 position = playerTransform.position;
            position.y = StageStartPosition;
            playerTransform.position = position;
            if (chasePlayer)
                RunAndChasePlayer(_stageRunningPlayer);
            else
                _stageRunningPlayer.StartRun();
            return _stageRunningPlayer;
        }

        public Player RunPlayer(Vector2 position, bool chasePlayer = true)
        {
            var player = GetPlayer(position);
            if (chasePlayer)
                RunAndChasePlayer(player);
            else
                player.StartRun();
            return player;
        }

        public Player RunPlayerForNextStage()
        {
            if (SelectedPlayer != null)
            {
                _playerPosition = SelectedPlayer.transform.position;
            }

            var player = GetPlayer(_playerPosition);
            RunAndChasePlayer(player);
            return player;
        }

        /// <summary>
        /// 게임 캐릭터를 갖고 올 때 사용함.
        /// 갖고 올 때 매번 모델을 할당해주고 있음.
        /// 모델을 매번 할당하지 않고, 모델이 변경되는 로직 마다 바꿔주게 하는 것이 좋겠음. 물론 연출도 그때에 맞춰서 해주는 식.
        /// </summary>
        /// <param name="caster"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public Character.CharacterBase GetCharacter(CharacterBase caster)
        {
            if (caster is null)
                throw new ArgumentNullException(nameof(caster));

            var characters = GetComponentsInChildren<Character.CharacterBase>()
                .Where(c => c.Id == caster.Id);
            var character = characters?.FirstOrDefault();

            if (!(characters is null) && characters.Any())
            {
                var ch = characters.First();

                if (ch is null)
                {
                    Debug.Log("player is null");
                }
                if (ch is Player)
                {
                    character = characters.FirstOrDefault(x =>
                        x.GetComponent<SortingGroup>().sortingLayerName == "Character");
                }
            }
            character?.Set(caster);

            return character;
        }

        private void PlayBGVFX(bool isBoss)
        {
            if (isBoss && bosswaveBGVFX)
            {
                if (defaultBGVFX)
                    defaultBGVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                bosswaveBGVFX.Play(true);
            }
            else
            {
                if (bosswaveBGVFX)
                    bosswaveBGVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (defaultBGVFX && !defaultBGVFX.isPlaying)
                    defaultBGVFX.Play(true);
            }
        }

        private IEnumerator CoUnlockMenu()
        {
            var menuNames = new List<string>();

            if (stageId == GameConfig.RequireClearedStageLevel.UIMainMenuShop)
            {
                menuNames.Add("Shop");
            }

            if (stageId == GameConfig.RequireClearedStageLevel.UIMainMenuRankingBoard)
            {
                menuNames.Add(nameof(RankingBoard));
            }

            if (stageId == GameConfig.RequireClearedStageLevel.UIMainMenuMimisbrunnr)
            {
                menuNames.Add("Mimisbrunnr");
            }

            var celebratesPopup = Widget.Find<CelebratesPopup>();
            foreach (var menuName in menuNames)
            {
                celebratesPopup.Show(menuName);
                yield return new WaitWhile(() => celebratesPopup.IsActive());
            }
        }

        private static void RunAndChasePlayer(Character.CharacterBase player)
        {
            player.StartRun();
            ActionCamera.instance.ChaseX(player.transform);
        }
    }
}
