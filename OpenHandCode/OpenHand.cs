using Godot;
using Godot.NativeInterop;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;


namespace OpenHand.OpenHandCode;


public class OpenHand
{
    
    public static void patch()
    {
        var harmony = new Harmony("com.OpenHand.OpenHand");
        harmony.PatchAll();
    }
}


[HarmonyPatch("UpdateHighlightedState")]
[HarmonyPatch(typeof(NMultiplayerPlayerState))]
internal class NMultiplayerPlayerStateOpenHand
{
    private static readonly Control CardContainer = SceneHelper.Instantiate<Control>("CardContainer");
    private static NGame? instance = NGame.Instance;
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        AccessTools.FieldRef<NMultiplayerPlayerState, bool> _mouseOverRef = AccessTools.FieldRefAccess<NMultiplayerPlayerState, bool>("_isMouseOver");
        if (_mouseOverRef(__instance) && !LocalContext.IsMe(__instance.Player))
        {
            //Set the height of the displayed cards to the hovered Player's HP bar.
            if (Traverse.Create(__instance).Field("_healthBar").GetValue() is NHealthBar hpBarRef)
                CardContainer.GlobalPosition = new Vector2(hpBarRef.GlobalPosition.X + 280f, hpBarRef.GlobalPosition.Y);

            if (CardContainer.GetParent() == null)
            {
                instance?.AddChild(CardContainer);
                CardContainer.AddToGroup("CardContainers");
            }

            //Very odd solution, create own custom property
            CardContainer.AccessibilityName = __instance.Player.NetId.ToString();
            
            if(!CardContainerHandler.AllCardsVisible)
                CardContainerHandler.GetSetCards(__instance);
            
            FadeInCards(CardContainer, 0.25f);
            CardContainer.Visible = true;
        }
        else
        {
            FadeOutCards(CardContainer, 0.25f, 0.0f);
            CardContainer.Visible = false;
            CardContainer.GetNode("HBoxContainer").FreeChildren();
        }
    }

    private static Tween? cardTween;
    private static void FadeOutCards(Control cards, float duration, float finalAlpha)
    {
        cardTween?.Kill();
        cardTween = cards.CreateTween();
        cardTween.TweenProperty((GodotObject) cards, (NodePath) "modulate:a", (Variant) finalAlpha, (double) duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private static void FadeInCards(Control cards, float duration)
    {
        cardTween?.Kill();
        cardTween = cards.CreateTween();
        cardTween.TweenProperty((GodotObject) cards, (NodePath) "modulate:a", (Variant) 1f, (double) duration);
    }
}

[HarmonyPatch("RefreshCombatValues")]
[HarmonyPatch(typeof(NMultiplayerPlayerState))]
internal class RefreshCombatValuesOpenHand
{
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        CardContainerHandler.GetSetCards(__instance);
    }
}

internal static class CardContainerHandler
{
    private static NGame? instance = NGame.Instance;
    private static Control? CardContainer;
    public static Vector2 CardSize = new Vector2(320, 440);

    public static bool AllCardsVisible { get; set; } = true;

    public static void GetSetCards(NMultiplayerPlayerState __instance)
    {
        CardContainer = (Control)instance.GetTree().GetFirstNodeInGroup("CardContainers");
        if (CardContainer != null && __instance.Player.NetId.ToString().Equals(CardContainer.AccessibilityName))
        {
            CardContainer.GetNode("HBoxContainer").FreeChildren();
            IReadOnlyList<CardModel> otherHand = PileType.Hand.GetPile(__instance.Player).Cards;
            foreach (CardModel c in otherHand)
            {
                NCard? display = NCard.Create(c);
                display?.SetCustomMinimumSize(CardContainerHandler.CardSize);
                CardContainer.GetNode("HBoxContainer").AddChild(display);
                display?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            }
        }
    }
}

[HarmonyPatch("OnTurnStarted")]
[HarmonyPatch(typeof(NMultiplayerPlayerState))]
internal class OnTurnStartedOpenHand
{
    private static readonly Control StartContainer = SceneHelper.Instantiate<Control>("CombatStart");
    private static readonly NGame? Instance = NGame.Instance;
    
    private static Tween? _boxTween;
    private async static void Postfix(NMultiplayerPlayerState __instance)
    {
        if (StartContainer.GetParent() == null)
        {
            Instance?.AddChild(StartContainer);
            StartContainer.Position = new Vector2(360, 260);
        }
        
        VBoxContainer allCards = (VBoxContainer)StartContainer.GetNode("VBoxContainer");
        allCards.FreeChildren();
        var players = __instance.Player.Creature.CombatState?.Players;
        foreach (Player id in players)
        {
            if (LocalContext.IsMe(id)) continue;
            var cards = PileType.Hand.GetPile(id).Cards;
            HBoxContainer newHBox = new HBoxContainer();
            newHBox.Size = CardContainerHandler.CardSize;
            newHBox.SetCustomMinimumSize(CardContainerHandler.CardSize);
            newHBox.SetMouseFilter(Control.MouseFilterEnum.Ignore);
            allCards.AddChild(newHBox);
            foreach (CardModel c in cards)
            {
                NCard? display = NCard.Create(c);
                display?.SetCustomMinimumSize(CardContainerHandler.CardSize);
                newHBox.AddChild(display);
                display?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            }
        }
        

        const int tweenDur = 1;

        _boxTween?.Kill();
        _boxTween = StartContainer.CreateTween();
        allCards.Modulate = new Color(allCards.Modulate.R, allCards.Modulate.G, allCards.Modulate.B,0f);
        CardContainerHandler.AllCardsVisible = true;
        _boxTween.TweenProperty((GodotObject) allCards, (NodePath) "modulate:a", 1f, tweenDur).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        _boxTween.TweenInterval(3.0f);
        _boxTween.TweenProperty((GodotObject) allCards, (NodePath) "modulate:a", 0f, tweenDur).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Expo);
        _boxTween.TweenCallback(Callable.From(() => CardContainerHandler.AllCardsVisible = false));
    }
}


