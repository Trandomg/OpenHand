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
                instance?.AddChild(CardContainer);
            
            IReadOnlyList<CardModel> otherHand = PileType.Hand.GetPile(__instance.Player).Cards;
            
            foreach (CardModel c in otherHand)
            {
                NCard? display = NCard.Create(c);
                display?.SetCustomMinimumSize(new Vector2(320,320));
                CardContainer.GetNode("HBoxContainer").AddChild(display);
                display?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            }
            
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

