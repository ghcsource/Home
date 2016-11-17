/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
package com.walkmap;

import android.app.Application;
import android.provider.Settings.Secure;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import org.apache.http.HttpResponse;
import org.apache.http.HttpStatus;
import org.apache.http.client.entity.UrlEncodedFormEntity;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.message.BasicNameValuePair;
import org.apache.http.protocol.HTTP;
import org.apache.http.util.EntityUtils;

/**
 *
 * @author ghc
 */
class HttpUtility
{

    public static String GetHttpResponse(String url, List<BasicNameValuePair> dataSet) throws IOException
    {
        HttpPost post = new HttpPost(url);
        UrlEncodedFormEntity entity = new UrlEncodedFormEntity(dataSet, HTTP.UTF_8);
        post.setEntity(entity);

        DefaultHttpClient httpClient = new DefaultHttpClient();
        HttpResponse response = httpClient.execute(post);

        int statusCode = response.getStatusLine().getStatusCode();

        if (statusCode != HttpStatus.SC_OK)
        {
            throw new RuntimeException(Integer.toString(statusCode));
        }

        String result = EntityUtils.toString(response.getEntity());
        return result;
    }
}

class UniqueIdUtility
{

    public static String GetUniqueId()
    {
        String uniqueId = Secure.getString(Common.application.getContentResolver(), Secure.ANDROID_ID);
        return uniqueId;
    }
}

class ResourceUtility
{
    public static String GetString(int resourceId)
    {
        return Common.application.getString(resourceId);
    }
}

class Common
{

    public static Application application = getApplicationUsingReflection();

    private static Application getApplicationUsingReflection()
    {
        Application result = null;
        try
        {
            result = (Application) Class.forName("android.app.AppGlobals").getMethod("getInitialApplication").invoke(null, (Object[]) null);
        }
        catch (Exception ex)
        {

        }
        return result;
    }
}

class LimitedQueue<T> implements Iterable<T>
{

    public LimitedQueue(int len)
    {
        if (len < 2)
        {
            throw new RuntimeException();
        }
        length = len;
        container = (T[]) new Object[len];
        index = -1;
    }

    private final int length;

    private T[] container;

    private int index;

    public void Push(T obj)
    {
        if (index == length - 1)
        {
            T[] temp = (T[]) new Object[length];
            System.arraycopy(container, 1, temp, 0, length - 1);
            container = temp;
            index--;
        }

        index++;
        container[index] = obj;
    }

    public List<T> Retrive()
    {
        List<T> list = new ArrayList<T>();
        for (T item : this)
        {
            list.add(item);
        }
        return list;
    }

    public void Clear()
    {
        index = -1;
        container = (T[]) new Object[length];
    }

    public Iterator<T> iterator()
    {
        return new LimitedIterator<T>();
    }

    class LimitedIterator<E> implements Iterator<E>
    {

        private int nextIndex;

        public LimitedIterator()
        {
            this.nextIndex = 0;
        }

        public boolean hasNext()
        {
            if (nextIndex <= LimitedQueue.this.index)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public E next()
        {
            E result = (E) LimitedQueue.this.container[nextIndex];
            nextIndex++;
            return result;
        }

        public void remove()
        {
            throw new UnsupportedOperationException("Not supported yet."); //To change body of generated methods, choose Tools | Templates.
        }

    }
}

class GPSInfo
{

    public GPSInfo(double latitude, double longitude, String positionSource)
    {
        Latitude = latitude;
        Longitude = longitude;
        PositionSource = positionSource;
    }

    public double Latitude;
    public double Longitude;
    public String PositionSource;
}
