## Run 2026-04-28T16:42Z - IRONCLAD

- Halt reason: manual
- End floor: 1, hp 80/80, last screen Room:Event
- Duration: 0.0 min, 1 commands, last id 13058

### Stability findings
- None.

### trace.log tail (last 100 lines)
```
2026-04-28T16:10:18.4504400Z NEventRoom.Proceed postfix fired
2026-04-28T16:10:18.4505826Z RequestWrite trigger=EventProceed-ClearPayload currentScreen=Map
2026-04-28T16:10:18.4570985Z WroteState revision=139 trigger=EventProceed-ClearPayload screen=Map
2026-04-28T16:10:18.4572805Z DispatchProceed via NEventRoom.Proceed
2026-04-28T16:10:18.4572951Z NEventRoom.Proceed completed
2026-04-28T16:10:18.4574788Z RequestWrite trigger=PostDispatch:Proceed currentScreen=Map
2026-04-28T16:10:18.4639757Z WroteState revision=140 trigger=PostDispatch:Proceed screen=Map
2026-04-28T16:10:18.4646723Z BridgeCommandReader wrote result id=13057 status=ok
2026-04-28T16:39:04.3253260Z Initialize start
2026-04-28T16:39:04.3261670Z RequestWrite trigger=Initialize currentScreen=<null>
2026-04-28T16:39:04.3350611Z WroteState revision=1 trigger=Initialize screen=<null>
2026-04-28T16:39:04.3352569Z BridgeMainThreadDispatcher: using Harmony pump strategy (NRun._Process / NControllerManager._Process)
2026-04-28T16:39:04.3358443Z BridgeCommandReader started; watching C:\Users\hi\AppData\Roaming\SlayTheSpire2\hermesbridge\commands.json
2026-04-28T16:39:04.3364772Z BridgeCommandReader dispatching id=13057 type=Proceed
2026-04-28T16:39:04.6460767Z BridgeSingleton ctor
2026-04-28T16:39:05.2993808Z BridgeMainThreadDispatcher.Pump first tick
2026-04-28T16:39:05.3008945Z RequestWrite trigger=PostDispatch:Proceed currentScreen=<null>
2026-04-28T16:39:05.3079736Z WroteState revision=2 trigger=PostDispatch:Proceed screen=<null>
2026-04-28T16:39:05.3142639Z BridgeCommandReader wrote result id=13057 status=error
2026-04-28T16:39:05.6598958Z MainMenu _Ready postfix fired
2026-04-28T16:39:05.6601942Z ClearTransientPayloads reason=MainMenuReady
2026-04-28T16:39:05.6604081Z SetScreen screen=MainMenu trigger=MainMenuReady
2026-04-28T16:39:05.6605707Z RequestWrite trigger=MainMenuReady currentScreen=MainMenu
2026-04-28T16:39:05.6666447Z WroteState revision=3 trigger=MainMenuReady screen=MainMenu
2026-04-28T16:42:14.1245146Z BridgeCommandReader dispatching id=13058 type=StartRun
2026-04-28T16:42:14.1410656Z DispatchStartRun: empty seed, synthesized '12064795584703650412'
2026-04-28T16:42:14.1732437Z DispatchStartRun character=IRONCLAD seed='12064795584703650412' asc=0 mode=Standard
2026-04-28T16:42:14.1734905Z RequestWrite trigger=PostDispatch:StartRun currentScreen=MainMenu
2026-04-28T16:42:14.1803515Z WroteState revision=4 trigger=PostDispatch:StartRun screen=MainMenu
2026-04-28T16:42:14.1818051Z BridgeCommandReader wrote result id=13058 status=ok
2026-04-28T16:42:15.2339801Z MapScreen _Ready postfix fired
2026-04-28T16:42:15.2342884Z SetScreen screen=Map trigger=MapScreenReady
2026-04-28T16:42:15.2344548Z RequestWrite trigger=MapScreenReady currentScreen=Map
2026-04-28T16:42:15.2424357Z WroteState revision=5 trigger=MapScreenReady screen=Map
2026-04-28T16:42:15.8128040Z NMapScreen.Close postfix fired
2026-04-28T16:42:15.8131125Z SetScreen screen=MapClosed trigger=MapScreenClose
2026-04-28T16:42:15.8132772Z RequestWrite trigger=MapScreenClose currentScreen=MapClosed
2026-04-28T16:42:15.8199014Z WroteState revision=6 trigger=MapScreenClose screen=MapClosed
2026-04-28T16:42:15.9900856Z NMapScreen.SetMap postfix fired (map=<ok>)
2026-04-28T16:42:15.9934840Z RequestWrite trigger=MapScreenSetMap currentScreen=MapClosed
2026-04-28T16:42:16.0031686Z WroteState revision=7 trigger=MapScreenSetMap screen=MapClosed
2026-04-28T16:42:16.0063539Z NMapScreen.Close postfix fired
2026-04-28T16:42:16.0065338Z SetScreen screen=MapClosed trigger=MapScreenClose
2026-04-28T16:42:16.0066736Z RequestWrite trigger=MapScreenClose currentScreen=MapClosed
2026-04-28T16:42:16.0131499Z WroteState revision=8 trigger=MapScreenClose screen=MapClosed
2026-04-28T16:42:16.0673444Z Hook BeforeRoomEntered roomType=Event id=EVENT:NEOW
2026-04-28T16:42:16.2989133Z NEventRoom._Ready postfix fired
2026-04-28T16:42:16.2992072Z SetScreen screen=Event trigger=EventRoomReady
2026-04-28T16:42:16.2994122Z RequestWrite trigger=EventRoomReady currentScreen=Event
2026-04-28T16:42:16.3084774Z WroteState revision=9 trigger=EventRoomReady screen=Event
2026-04-28T16:42:16.3088156Z PushEvent(EventRoomReady) skipped: no event on EventRoom
2026-04-28T16:42:16.3103590Z Hook AfterRoomEntered roomType=Event
2026-04-28T16:42:16.3105295Z SetScreen screen=Room:Event trigger=AfterRoomEntered
2026-04-28T16:42:16.3106697Z RequestWrite trigger=AfterRoomEntered currentScreen=Room:Event
2026-04-28T16:42:16.3169173Z WroteState revision=10 trigger=AfterRoomEntered screen=Room:Event
2026-04-28T16:42:16.3204325Z BugT[diag]: EC.METH0 Int32 GetAmountToSpend() = 1
2026-04-28T16:42:16.3206533Z BugT[diag]: EC.METH0 Int32 GetResolved() = 1
2026-04-28T16:42:16.3209239Z BugT[diag]: EC.METH0 Boolean EndOfTurnCleanup() = False
2026-04-28T16:42:16.3211409Z BugT[diag]: EC.METH0 Boolean AfterCardPlayedCleanup() = False
2026-04-28T16:42:16.3213417Z BugT[diag]: EC.METH0 Void FinalizeUpgrade() = <null>
2026-04-28T16:42:16.3215661Z BugT[diag]: EC.METH0 Void ResetForDowngrade() = <null>
2026-04-28T16:42:16.3217896Z BugT[diag]: EC.FIELD CardModel _card = CARD.STRIKE_IRONCLAD (15827222)
2026-04-28T16:42:16.3219511Z BugT[diag]: EC.FIELD Int32 _base = 1
2026-04-28T16:42:16.3220913Z BugT[diag]: EC.FIELD Int32 _capturedXValue = 0
2026-04-28T16:42:16.3222832Z BugT[diag]: EC.FIELD List`1 _localModifiers = []
2026-04-28T16:42:16.3224350Z BugT[diag]: EC.FIELD Int32 <Canonical>k__BackingField = 1
2026-04-28T16:42:16.3225809Z BugT[diag]: EC.FIELD Boolean <CostsX>k__BackingField = False
2026-04-28T16:42:16.3227361Z BugT[diag]: EC.FIELD Boolean <WasJustUpgraded>k__BackingField = False
2026-04-28T16:42:16.3229052Z BugT[diag]: EC.PROP  Int32 Canonical = 1
2026-04-28T16:42:16.3230988Z BugT[diag]: EC.PROP  Boolean CostsX = False
2026-04-28T16:42:16.3232585Z BugT[diag]: EC.PROP  Boolean WasJustUpgraded = False
2026-04-28T16:42:16.3234857Z BugT[diag]: EC.PROP  Boolean HasLocalModifiers = False
2026-04-28T16:42:16.3241635Z BugT[diag]: CARD.METH0 Void InvokeEnergyCostChanged() = <null>
2026-04-28T16:42:16.3246087Z BugT[diag]: CARD.METH0 Int32 GetStarCostThisCombat() = -1
2026-04-28T16:42:16.3250369Z BugT[diag]: CARD.METH0 Int32 GetStarCostWithModifiers() = -1
2026-04-28T16:42:16.3253160Z BugT[diag]: CARD.PROP  Int32 CanonicalEnergyCost = 1
2026-04-28T16:42:16.3255102Z BugT[diag]: CARD.PROP  Boolean HasEnergyCostX = False
2026-04-28T16:42:16.3256995Z BugT[diag]: CARD.PROP  CardEnergyCost EnergyCost = MegaCrit.Sts2.Core.Entities.Cards.CardEnergyCost
2026-04-28T16:42:16.3258877Z BugT[diag]: CARD.PROP  Int32 CanonicalStarCost = -1
2026-04-28T16:42:16.3260972Z BugT[diag]: CARD.PROP  Int32 BaseStarCost = -1
2026-04-28T16:42:16.3263073Z BugT[diag]: CARD.PROP  Boolean WasStarCostJustUpgraded = False
2026-04-28T16:42:16.3265459Z BugT[diag]: CARD.PROP  TemporaryCardCost TemporaryStarCost = <null>
2026-04-28T16:42:16.3267491Z BugT[diag]: CARD.PROP  Int32 CurrentStarCost = -1
2026-04-28T16:42:16.3269336Z BugT[diag]: CARD.PROP  Boolean HasStarCostX = False
2026-04-28T16:42:16.3345438Z FloorHistory: truncated stale file at startup.
2026-04-28T16:42:16.3358724Z RequestWrite trigger=AfterRoomEntered currentScreen=Room:Event
2026-04-28T16:42:16.3486986Z WroteState revision=11 trigger=AfterRoomEntered screen=Room:Event
2026-04-28T16:42:16.3496088Z BugEv[diag]: event type = MegaCrit.Sts2.Core.Models.Events.Neow
2026-04-28T16:42:16.3498153Z BugEv[diag]: Event.Description.Variables.Count=0
2026-04-28T16:42:16.3602598Z RequestWrite trigger=AfterRoomEntered currentScreen=Room:Event
2026-04-28T16:42:16.3680920Z WroteState revision=12 trigger=AfterRoomEntered screen=Room:Event
2026-04-28T16:42:16.3682847Z RequestWrite trigger=AfterRoomEntered-ClearShop currentScreen=Room:Event
2026-04-28T16:42:16.3745958Z WroteState revision=13 trigger=AfterRoomEntered-ClearShop screen=Room:Event
2026-04-28T16:42:16.3748681Z RequestWrite trigger=AfterRoomEntered-ClearRestSite currentScreen=Room:Event
2026-04-28T16:42:16.3809587Z WroteState revision=14 trigger=AfterRoomEntered-ClearRestSite screen=Room:Event
2026-04-28T16:42:16.3812012Z RequestWrite trigger=AfterRoomEntered-ClearTreasure currentScreen=Room:Event
2026-04-28T16:42:16.3879781Z WroteState revision=15 trigger=AfterRoomEntered-ClearTreasure screen=Room:Event
2026-04-28T16:42:16.3886603Z RequestWrite trigger=AfterRoomEntered currentScreen=Room:Event
2026-04-28T16:42:16.3947882Z WroteState revision=16 trigger=AfterRoomEntered screen=Room:Event
2026-04-28T16:42:16.6335414Z StartNewSingleplayerRun completed OK
```


