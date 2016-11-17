/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
package com.walkmap;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Bundle;
import android.os.Looper;
import android.os.PowerManager;
import android.os.PowerManager.WakeLock;
import android.os.SystemClock;
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import org.apache.http.message.BasicNameValuePair;

/**
 *
 * @author ghc
 */
public class AlarmReceiver extends BroadcastReceiver
{
    public static PendingIntent pi = PendingIntent.getBroadcast(Common.application, 0, new Intent(Common.application, AlarmReceiver.class), 0);
    
    @Override
    public void onReceive(Context context, Intent intent)
    {
        PowerManager powerManager = (PowerManager)Common.application.getSystemService(Context.POWER_SERVICE);
        final WakeLock wakeLock = powerManager.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "");
        wakeLock.acquire();
        
        new Thread(new Runnable()
        {
            public void run()
            {
                try
                {
                    Looper.prepare();
                    final Looper looper = Looper.myLooper();
                    
                    final LocationManager locationManager = (LocationManager)Common.application.getSystemService(Context.LOCATION_SERVICE);
                    int count = 0;
                    if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER) == true)
                    {
                        count++;
                    }
                    if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER) == true)
                    {
                        count++;
                    }
                    final CountDownLatch counter = new CountDownLatch(count);
                    
                    final NetworkLocationListener networkLocationListener = new NetworkLocationListener(counter);
                    final GPSLocationListener gpsLocationListener = new GPSLocationListener(counter);

                    if(locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER) == true)
                    {
                        locationManager.requestLocationUpdates(LocationManager.NETWORK_PROVIDER, 1000, 0, networkLocationListener, looper);
                    }
                    if(locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER) == true)
                    {
                        locationManager.requestLocationUpdates(LocationManager.GPS_PROVIDER, 1000, 0, gpsLocationListener, looper);
                    }
                    
                    new Thread(new Runnable()
                    {
                        public void run()
                        {
                            try
                            {
                                counter.await(60, TimeUnit.SECONDS);
                                locationManager.removeUpdates(networkLocationListener);
                                locationManager.removeUpdates(gpsLocationListener);
                            }
                            catch(Exception ex)
                            {
                            }
                            finally
                            {
                                looper.quit();
                            }
                        }
                    }).start();
                    
                    Looper.loop();
                }
                finally
                {
                    wakeLock.release();
                }
            }
        }).start();
    }

    public static void SetAlarm()
    {
        AlarmManager am = (AlarmManager) Common.application.getSystemService(Context.ALARM_SERVICE);
        am.cancel(pi);
        am.setRepeating(AlarmManager.ELAPSED_REALTIME_WAKEUP, SystemClock.elapsedRealtime(), 15 * 60 * 1000, pi);
        
        SharedPreferences settings = Common.application.getSharedPreferences("Store", Context.MODE_PRIVATE);
        SharedPreferences.Editor editor = settings.edit();
        editor.putBoolean("Binding", true);
        editor.commit();
    }

    public static void CancelAlarm()
    {
        AlarmManager am = (AlarmManager) Common.application.getSystemService(Context.ALARM_SERVICE);
        am.cancel(pi);
        
        SharedPreferences settings = Common.application.getSharedPreferences("Store", Context.MODE_PRIVATE);
        SharedPreferences.Editor editor = settings.edit();
        editor.putBoolean("Binding", false);
        editor.commit();
    }
    
    public class GPSLocationListener extends LocationListenerBase
    {
        private Location baseLocation = null;

        public GPSLocationListener(CountDownLatch counter) 
        {
            super(counter);
        }

        public void onLocationChanged(Location location) 
        {
            if(location == null)
            {
                return;
            }
            
            if(baseLocation == null)
            {
                baseLocation = location;
                return;
            }
            
            if(baseLocation.getLatitude() == location.getLatitude() && baseLocation.getLongitude() == location.getLongitude())
            {
                return;
            }
            
            try
            {
                SendLocation(location);
            }
            finally
            {
                RemoveListener();
                counter.countDown();
            }
        }
        
    }
    
    public class NetworkLocationListener extends LocationListenerBase
    {

        public NetworkLocationListener(CountDownLatch counter) 
        {
            super(counter);
        }

        public void onLocationChanged(Location location) 
        {
            
            try
            {
                SendLocation(location);
            }
            finally
            {
                RemoveListener();
                counter.countDown();
            }
        }
        
    }
    
    public abstract class LocationListenerBase implements LocationListener
    {
        protected CountDownLatch counter;

        public LocationListenerBase(CountDownLatch counter)
        {
            this.counter = counter;
        }
        
        protected void RemoveListener()
        {
            LocationManager locationManager = (LocationManager)Common.application.getSystemService(Context.LOCATION_SERVICE);
            locationManager.removeUpdates(this);
        }
        
        protected void SendLocation(Location location)
        {
            if(location == null)
            {
                return;
            }
            
            ///////////////////////////////////
            if(BuildConfig.DEBUG)
            {
                MyNotify.Show(Double.toString(location.getLatitude()) + " | " + Double.toString(location.getLongitude())  + " | " + location.getProvider());
            }
            ///////////////////////////////////

            SharedPreferences settings = Common.application.getSharedPreferences("Store", Context.MODE_PRIVATE);
            LimitedQueue<GPSInfo> queue;
            if (settings.getString("Queue", null) != null)
            {
                String queueString = settings.getString("Queue", null);
                Gson gson = new Gson();
                queue = gson.fromJson(queueString, new TypeToken<LimitedQueue<GPSInfo>>()
                {
                }.getType());
            }
            else
            {
                queue = new LimitedQueue<GPSInfo>(20);
            }

            queue.Push(new GPSInfo(location.getLatitude(), location.getLongitude(), location.getProvider()));

            try
            {
                String deviceUniqueId = UniqueIdUtility.GetUniqueId();

                List<GPSInfo> gpsInfoList = queue.Retrive();
                Gson gson = new Gson();
                String gpsInfoListJson = gson.toJson(gpsInfoList);

                List<BasicNameValuePair> postData = new ArrayList<BasicNameValuePair>();
                postData.add(new BasicNameValuePair("deviceUniqueId", deviceUniqueId));
                postData.add(new BasicNameValuePair("gpsInfoList", gpsInfoListJson));
                HttpUtility.GetHttpResponse(ResourceUtility.GetString(R.string.SendPositionAPI), postData);

                queue.Clear();
            }
            catch (Exception ex)
            { }
            finally
            {
                Gson gson = new Gson();
                String queueString = gson.toJson(queue);
                SharedPreferences.Editor editor = settings.edit();
                editor.putString("Queue", queueString);
                editor.commit();
            }
        }

        public void onStatusChanged(String provider, int status, Bundle extras)
        {

        }

        public void onProviderEnabled(String provider)
        {

        }

        public void onProviderDisabled(String provider)
        {

        }

    }
}

class MyNotify
{
    public static void Show(String message)
    {
        NotificationManager nm = (NotificationManager) Common.application.getSystemService(Context.NOTIFICATION_SERVICE);
        Intent notificationIntent = new Intent(Common.application, MainActivity.class);
        PendingIntent contentIntent = PendingIntent.getActivity(Common.application, 0, notificationIntent, 0);
        Notification.Builder builder =new Notification.Builder(Common.application);    
        builder.setContentIntent(contentIntent);
        builder.setSmallIcon(R.drawable.ic_launcher);
        builder.setContentTitle(message);
        Notification n = builder.getNotification();
        n.defaults =Notification.DEFAULT_SOUND;
        nm.notify((int) (System.currentTimeMillis() % Integer.MAX_VALUE), n);
    }
}
