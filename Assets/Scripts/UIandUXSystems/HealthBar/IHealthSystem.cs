/*
Written by Brandon Wahl

Interface that allows any creature (player or enemy) to update their health

*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public interface IHealthSystem
{
 
    void HealHP(float hp);

    void LoseHP(float damage, float rumbleDuration, float lowFrequency, float highFrequency);

    float currentHP { get; }

    float maxHP { get; } 
    
}
