/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
package com.walkmap;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;

/**
 *
 * @author ghc
 */
public class BootReceiver extends BroadcastReceiver
{

    @Override
    public void onReceive(Context context, Intent intent)
    {
        SharedPreferences settings = Common.application.getSharedPreferences("Store", Context.MODE_PRIVATE);
        if (settings.getBoolean("Binding", false) == true)
        {
            AlarmReceiver.SetAlarm();
        }
    }

}
