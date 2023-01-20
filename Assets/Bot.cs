using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bot : MonoBehaviour
{
    public CardPlayer player;
    public CardGameManager gameManager;
    public BotStats stats;

    private float timer = 0;

    int lastSelected = 0;
    Cards[] cards;
    public bool IsReady = false;

    public void SetStats(BotStats newStats, bool restoreFullHealth = false)
    {
        this.stats = newStats;
        
        var newPlayrStats = new PlayerStats
        {
            MaxHealth = this.stats.MaxHealth,
            RestoreValue = this.stats.RestoreValue,
            DamageValue = this.stats.DamageValue,
        };

        player.SetStats(newPlayrStats, restoreFullHealth);
    }

    IEnumerator Start()
    {
        cards = player.GetComponentsInChildren<Cards>();

        yield return new WaitUntil(()=>player.IsReady);
        SetStats(this.stats);
        this.IsReady = true;
    }


    void Update()
    {
        if (gameManager.State != CardGameManager.GameState.ChooseAttack)
        {
            timer = 0;
            return;
        }
        if (timer < stats.ChoosingInterval)
        {
            timer += Time.deltaTime;
            return;
        }

        timer = 0;
        ChooseAttack();
    }

    public void ChooseAttack()
    {
        var random = Random.Range(1, cards.Length);
        var selection = (lastSelected + random) % cards.Length;
        //last + random % length = value
        //(0 + 1 ) % 3 = 1
        //(0 + 2 ) % 3 = 2
        //(0 + 1 ) % 3 = 2
        //(0 + 2 ) % 3 = 0
        //(0 + 1 ) % 3 = 0
        //(0 + 2 ) % 3 = 1
        player.SetChosenCard(cards[selection]);
        lastSelected = selection;
    }
}
