using UnityEngine;
using TMPro;
//Written By Brandon
public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] [Range(1, 10 )] private int amountOfHPEffected;
    [SerializeField] private TMP_Text healthText;

    private void Update()
    {
        var health = FindPlayerHealth();
        if (health == null || healthText == null)
            return;

        healthText.text = Mathf.RoundToInt(health.CurrentHealth).ToString();
    }

    //Tests script to show healthbar functionality
    public void TakeDamage()
    {
        var health = FindPlayerHealth();
        if (health == null) return;
        health.LoseHP(amountOfHPEffected, 0, 0, 0);
        Debug.Log(health.CurrentHealth);
    }

    public void Heal()
    {
        var health = FindPlayerHealth();
        if (health == null) return;
        health.HealHP(amountOfHPEffected);
        Debug.Log(health.CurrentHealth);
    }

    private PlayerHealthBarManager FindPlayerHealth()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return null;

        return player.GetComponent<PlayerHealthBarManager>();
    }
}
