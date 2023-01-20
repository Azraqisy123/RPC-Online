using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class CardGameManager : MonoBehaviour, IOnEventCallback
{
    public GameObject netPlayerPrefab;
    public CardPlayer P1;
    public CardPlayer P2;
    public PlayerStats defaultPlayerStats = new PlayerStats
    {
        MaxHealth = 100,
        RestoreValue = 5,
        DamageValue = 10
    };
    
    public GameState State, NextState = GameState.NetPlayersIntialization;
    public GameObject gameOverPanel;
    public TMP_Text winnerText;
    public TMP_Text pingText;
    private CardPlayer damagedPlayer;
    private CardPlayer winner;
    public  bool Online = true;

    //public List<int> syncReadyPlayers = new List<int>(2);

    HashSet<int> syncReadyPlayers = new HashSet<int>();
    public enum GameState
    {
        SyncState,
        NetPlayersIntialization,
        ChooseAttack,
        Attacks,
        Damages,
        Draw,
        GameOver,
    }

    private void Start()
    {
        gameOverPanel.SetActive(false);
        if (Online)
        {
            PhotonNetwork.Instantiate(netPlayerPrefab.name, Vector3.zero, Quaternion.identity);
            StartCoroutine(PingCoroutine());
            State = GameState.NetPlayersIntialization;
            NextState = GameState.NetPlayersIntialization;
            if(PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropertyNames.Room.RestoreValue, out var restoreValue))
            {
                defaultPlayerStats.RestoreValue = (float) restoreValue;
            }
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PropertyNames.Room.DamageValue, out var damageValue))
            {
                defaultPlayerStats.DamageValue = (float) damageValue;
            }    
        }
        else
        {
            State = GameState.ChooseAttack;
        }

        P1.SetStats(defaultPlayerStats, true);
        P2.SetStats(defaultPlayerStats, true);
        P1.IsReady = true;
        P2.IsReady = true;
    }

    private void Update()
    {

        switch (State)
        {
            case GameState.SyncState:
                if (syncReadyPlayers.Count == 2)
                {
                    syncReadyPlayers.Clear();
                    State = NextState;
                }
                break;

            case GameState.NetPlayersIntialization:
                if (CardNetPlayer.NetPlayers.Count == 2)
                {
                    foreach (var netPlayer in CardNetPlayer.NetPlayers)
                    {
                        if (netPlayer.photonView.IsMine)
                        {
                            netPlayer.Set(P1);
                        }
                        else
                        {
                            netPlayer.Set(P2);
                        }
                    }
                    ChangeState(GameState.ChooseAttack);
                }
                break;

            case GameState.ChooseAttack:
                if (P1.AttackValue != null && P2.AttackValue != null)
                {
                    P1.AnimateAttack();
                    P2.AnimateAttack();
                    P1.isClickable(false);
                    P2.isClickable(false);
                    ChangeState(GameState.Attacks);
                }
                break;

            case GameState.Attacks:
                if (P1.IsAnimating() == false && P2.IsAnimating() == false)
                {
                    damagedPlayer = GetDamagedPlayer();
                    if (damagedPlayer != null)
                    {
                        damagedPlayer.AnimateDamage();
                        ChangeState(GameState.Damages);
                    }
                    else
                    {
                        P1.AnimateDraw();
                        P2.AnimateDraw();
                        ChangeState(GameState.Draw);
                    }
                }
                break;


            case GameState.Damages:
                if (P1.IsAnimating() == false && P2.IsAnimating() == false)
                {
                    //calculate health
                    if (damagedPlayer == P1)
                    {
                        P1.ChangeHealth(-P2.stats.DamageValue);
                        P2.ChangeHealth(P2.stats.RestoreValue);
                    }
                    else
                    {
                        P2.ChangeHealth(-P1.stats.DamageValue);
                        P1.ChangeHealth(P1.stats.RestoreValue);
                    }

                    var winner = GetWinner();

                    if (winner == null)
                    {
                        ResetPlayers();
                        P1.isClickable(true);
                        P2.isClickable(true);
                        ChangeState(GameState.ChooseAttack);
                    }
                    else
                    {
                        Debug.Log(winner + " is Win");
                        gameOverPanel.SetActive(true);
                        winnerText.text = winner == P1 ?
                            $"{P1.NickName.text} wins" : $"{P2.NickName.text} wins";
                        ResetPlayers();
                        ChangeState(GameState.GameOver);
                    }


                }
                break;


            case GameState.Draw:
                if (P1.IsAnimating() == false && P2.IsAnimating() == false)
                {
                    ResetPlayers();
                    P1.isClickable(true);
                    P2.isClickable(true);
                    ChangeState(GameState.ChooseAttack);
                }
                break;
        }
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private const byte playerChangeState = 1;

    private void ChangeState(GameState newState)
    {
        if (Online == false)
        {
            State = newState;
            return;
        }

        if (this.NextState == newState)
            return;

        // kirim message bahwa kita ready
        var actorNum = PhotonNetwork.LocalPlayer.ActorNumber;
        var raiseEventOptions = new RaiseEventOptions();
        raiseEventOptions.Receivers = ReceiverGroup.All;
        PhotonNetwork.RaiseEvent(playerChangeState, actorNum, raiseEventOptions, SendOptions.SendReliable);

        // masuk ek state sync sebagai transisi sebelum state baru
        this.State = GameState.SyncState;
        this.NextState = newState;

    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case playerChangeState:
                var actorNum = (int)photonEvent.CustomData;
                // kalau pake HashSet ga perlu cek lagi
                syncReadyPlayers.Add(actorNum);
                break;
            default:
                break;

        }

    }


    IEnumerator PingCoroutine()
    {
        var wait = new WaitForSeconds(1);
        while (true)
        {
            pingText.text = "ping : " + PhotonNetwork.GetPing() + "ms";
            yield return wait;
        }
    }

    private void ResetPlayers()
    {
        damagedPlayer = null;
        P1.Reset();
        P2.Reset();
    }

    private CardPlayer GetDamagedPlayer()
    {
        Attack? PlayerAtk1 = P1.AttackValue;
        Attack? PlayerAtk2 = P2.AttackValue;

        if (PlayerAtk1 == Attack.Rock && PlayerAtk2 == Attack.Paper)
        {
            return P1;
        }
        else if (PlayerAtk1 == Attack.Rock && PlayerAtk2 == Attack.Scissor)
        {
            return P2;
        }
        else if (PlayerAtk1 == Attack.Paper && PlayerAtk2 == Attack.Rock)
        {
            return P2;
        }
        else if (PlayerAtk1 == Attack.Paper && PlayerAtk2 == Attack.Scissor)
        {
            return P1;
        }
        else if (PlayerAtk1 == Attack.Scissor && PlayerAtk2 == Attack.Rock)
        {
            return P1;
        }
        else if (PlayerAtk1 == Attack.Scissor && PlayerAtk2 == Attack.Paper)
        {
            return P2;
        }

        return null;
    }

    private CardPlayer GetWinner()
    {
        if (P1.Health == 0)
        {
            return P2;
        }
        else if (P2.Health == 0)
        {
            return P1;
        }
        else
        {
            return null;
        }
    }

    public void LoadScene(int SceneIndex)
    {
        SceneManager.LoadScene(SceneIndex);
    }

      public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        Debug.Log("Beralih Scene ke Scene " + sceneName);
    }
}
