using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUDTests
{
    [Test]
    public void PlayerHUD_UpdateShieldDisplay_UpdatesTextAndFill()
    {
        GameObject hudObj = new GameObject("TestHUD");
        PlayerHUD hud = hudObj.AddComponent<PlayerHUD>();

        GameObject textObj = new GameObject("ShieldText");
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        hud.shieldText = tmp;

        GameObject imgObj = new GameObject("ShieldFill");
        Image img = imgObj.AddComponent<Image>();
        img.type = Image.Type.Filled;
        hud.shieldProgress = img;

        hud.UpdateShieldDisplay(6f, 10f);

        Assert.AreEqual("SHIELD: 6 / 10", tmp.text, "Shield text should reflect current and max shield");
        Assert.AreEqual(0.6f, img.fillAmount, 0.001f, "Shield fill amount should reflect percentage");

        Object.DestroyImmediate(hudObj);
        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(imgObj);
    }

    [Test]
    public void PlayerHUD_UpdateHealthDisplay_UpdatesTextFillAndColor()
    {
        GameObject hudObj = new GameObject("TestHUD");
        PlayerHUD hud = hudObj.AddComponent<PlayerHUD>();

        GameObject textObj = new GameObject("HealthText");
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        hud.healthText = tmp;

        GameObject imgObj = new GameObject("HealthFill");
        Image img = imgObj.AddComponent<Image>();
        img.type = Image.Type.Filled;
        hud.healthProgress = img;

        // Full health
        hud.UpdateHealthDisplay(10f, 10f);
        Assert.AreEqual("HULL: 10 / 10", tmp.text);
        Assert.AreEqual(1.0f, img.fillAmount, 0.001f);
        Assert.Greater(img.color.g, img.color.r, "At full health, color should be predominantly green");

        // Low health (2 / 10)
        hud.UpdateHealthDisplay(2f, 10f);
        Assert.AreEqual("HULL: 2 / 10", tmp.text);
        Assert.AreEqual(0.2f, img.fillAmount, 0.001f);
        Assert.Greater(img.color.r, img.color.g, "At low health, color should shift toward red");

        Object.DestroyImmediate(hudObj);
        Object.DestroyImmediate(textObj);
        Object.DestroyImmediate(imgObj);
    }

    [Test]
    public void Player_TwoTierDamage_ShieldAbsorbsFirstThenHealth()
    {
        GameObject playerObj = new GameObject("TestPlayer");
        player p = playerObj.AddComponent<player>();
        p.shield_value = 8f;
        p.shield_max_value = 10f;
        p.health_value = 10f;
        p.health_max_value = 10f;

        // Damage absorbed fully by shield
        p.TakeDamage(5f);
        Assert.AreEqual(3f, p.shield_value, 0.001f, "Shield should be reduced to 3");
        Assert.AreEqual(10f, p.health_value, 0.001f, "Health should remain untouched at 10");

        // Damage breaks shield and penetrates to health
        p.TakeDamage(5f); // 3 shield + 2 health
        Assert.AreEqual(0f, p.shield_value, 0.001f, "Shield should be depleted to 0");
        Assert.AreEqual(8f, p.health_value, 0.001f, "Remaining 2 damage should penetrate to health");
        Assert.IsFalse(p.isDead, "Player should not be dead while health remains");

        // Lethal damage to health
        p.TakeDamage(8f);
        Assert.AreEqual(0f, p.health_value, 0.001f, "Health should reach 0");
        Assert.IsTrue(p.isDead, "Player should be dead when health is depleted");

        Object.DestroyImmediate(playerObj);
    }
}
