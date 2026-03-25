using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

enum CardState
{
    Cooling,
    Ready,
    WaitingSun
}

public class Card : MonoBehaviour
{
    private CardState cardState = CardState.Cooling;

    public GameObject sunflower;
    public GameObject sunflowerGray;
    public Image sunflowerMask;


    private void Update()
    {
        switch (cardState)
        {
            case CardState.Cooling:
                CoolingUpdate();
                break;
            case CardState.Ready:
                ReadyUpdate();
                break;
            case CardState.WaitingSun:
                WaitingSunUpdate();
                break;
            default:
                break;
        }
    }

    void CoolingUpdate()
    {

    }
    void ReadyUpdate()
    {

    }

    void WaitingSunUpdate()
    {

    }
}
