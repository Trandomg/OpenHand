using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using HarmonyLib;
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
    private static readonly Control StartContainer = SceneHelper.Instantiate<Control>("CombatStart");
    private static NGame? instance = NGame.Instance;
    private static Tween? _boxTween;
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
        
        
        VBoxContainer allCards = (VBoxContainer)StartContainer.GetNode("VBoxContainer");
        if (_mouseOverRef(__instance) && LocalContext.IsMe(__instance.Player))
        {
            if (StartContainer.GetParent() == null)
            {
                instance?.AddChild(StartContainer);
                StartContainer.Position = new Vector2(360, 260);
            }
        
            
            
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
            FadeInCards(StartContainer, 0.25f);
            StartContainer.Visible = true;
        }
        else
        {
            FadeOutCards(StartContainer, 0.25f, 0.0f);
            StartContainer.Visible = false;
            allCards.FreeChildren();
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


