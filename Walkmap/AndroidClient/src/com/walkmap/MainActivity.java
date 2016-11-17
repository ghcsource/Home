package com.walkmap;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.os.Bundle;
import android.text.Html;
import android.text.method.LinkMovementMethod;
import android.view.View;
import android.view.Window;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import com.google.gson.Gson;
import java.text.MessageFormat;
import java.util.ArrayList;
import java.util.List;
import org.apache.http.message.BasicNameValuePair;

public class MainActivity extends Activity
{

    /**
     * Called when the activity is first created.
     */
    @Override
    public void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        setContentView(R.layout.main);
        
        TextView textview = (TextView) findViewById(R.id.goReg);
        String goReg = MessageFormat.format("<a href=\"{0}\">{1}</a>", this.getString(R.string.Site), this.getString(R.string.GoReg));
        textview.setText(Html.fromHtml(goReg));
        textview.setMovementMethod(LinkMovementMethod.getInstance());

        ProgressBar progressBar = (ProgressBar) findViewById(R.id.progressBar);
        progressBar.setVisibility(View.VISIBLE);

        new Thread(new Runnable()
        {
            public void run()
            {
                CheckBinding();
            }
        }).start();
    }

    private void CheckBinding()
    {
        try
        {
            List<BasicNameValuePair> postData = new ArrayList<BasicNameValuePair>();
            postData.add(new BasicNameValuePair("deviceUniqueId", UniqueIdUtility.GetUniqueId()));

            final String owner = HttpUtility.GetHttpResponse(ResourceUtility.GetString(R.string.GetDeviceOwnerAPI), postData);

            if (!owner.equals(""))
            {
                AlarmReceiver.SetAlarm();
            }
            else
            {
                AlarmReceiver.CancelAlarm();
            }

            this.runOnUiThread(new Runnable()
            {
                public void run()
                {
                    if (owner.equals(""))
                    {
                        ((EditText) findViewById(R.id.userName)).setText("");
                        ((EditText) findViewById(R.id.userName)).setEnabled(true);
                        ((EditText) findViewById(R.id.deviceName)).setText("");
                        ((EditText) findViewById(R.id.deviceName)).setEnabled(true);
                        ((Button) findViewById(R.id.bindButton)).setEnabled(true);
                        ((Button) findViewById(R.id.unbindButton)).setEnabled(false);
                    }
                    else
                    {
                        Gson gson = new Gson();
                        String[] ownerAndDeviceName = gson.fromJson(owner, String[].class);
                        ((EditText) findViewById(R.id.userName)).setText(ownerAndDeviceName[0]);
                        ((EditText) findViewById(R.id.userName)).setEnabled(false);
                        ((EditText) findViewById(R.id.deviceName)).setText(ownerAndDeviceName[1]);
                        ((EditText) findViewById(R.id.deviceName)).setEnabled(false);
                        ((Button) findViewById(R.id.bindButton)).setEnabled(false);
                        ((Button) findViewById(R.id.unbindButton)).setEnabled(true);
                    }

                    ((LinearLayout) findViewById(R.id.bindingView)).setVisibility(View.VISIBLE);
                }
            });
        }
        catch (Exception ex)
        {
            this.runOnUiThread(new Runnable()
            {
                public void run()
                {
                    ((LinearLayout) findViewById(R.id.bindingView)).setVisibility(View.GONE);
                    ((LinearLayout) findViewById(R.id.errorView)).setVisibility(View.VISIBLE);
                }
            });
        }
        finally
        {
            this.runOnUiThread(new Runnable()
            {
                public void run()
                {
                    ((ProgressBar) findViewById(R.id.progressBar)).setVisibility(View.GONE);
                }
            });
        }
    }

    public void unbindButton_Click(View view)
    {
        ((EditText) findViewById(R.id.userName)).setEnabled(false);
        ((EditText) findViewById(R.id.deviceName)).setEnabled(false);
        ((Button) findViewById(R.id.bindButton)).setEnabled(false);
        ((Button) findViewById(R.id.unbindButton)).setEnabled(false);
        ((ProgressBar) findViewById(R.id.progressBar)).setVisibility(View.VISIBLE);
        new Thread(new Runnable()
        {
            public void run()
            {
                Unbinding();
            }
        }).start();
    }

    private void Unbinding()
    {
        try
        {
            List<BasicNameValuePair> postData = new ArrayList<BasicNameValuePair>();
            postData.add(new BasicNameValuePair("deviceUniqueId", UniqueIdUtility.GetUniqueId()));
            HttpUtility.GetHttpResponse(ResourceUtility.GetString(R.string.UnbindDeviceAPI), postData);
        }
        catch (Exception ex)
        {

        }

        CheckBinding();
    }

    public void bindButton_Click(View view)
    {
        final String userName = ((EditText) findViewById(R.id.userName)).getText().toString().trim();
        final String deviceName = ((EditText) findViewById(R.id.deviceName)).getText().toString().trim();

        if (userName.equals("") || deviceName.equals(""))
        {
            new AlertDialog.Builder(this)
                    .setMessage(this.getString(R.string.UserDeviceNameNotNull))
                    .setPositiveButton(android.R.string.yes, new DialogInterface.OnClickListener()
                    {
                        public void onClick(DialogInterface dialog, int which)
                        {
                        }
                    })
                    .show();

            return;
        }

        ((EditText) findViewById(R.id.userName)).setEnabled(false);
        ((EditText) findViewById(R.id.deviceName)).setEnabled(false);
        ((Button) findViewById(R.id.bindButton)).setEnabled(false);
        ((Button) findViewById(R.id.unbindButton)).setEnabled(false);
        ((ProgressBar) findViewById(R.id.progressBar)).setVisibility(View.VISIBLE);

        new Thread(new Runnable()
        {
            public void run()
            {
                Binding(userName, deviceName);
            }
        }).start();
    }

    private void Binding(final String userName, final String deviceName)
    {
        try
        {
            List<BasicNameValuePair> postData = new ArrayList<BasicNameValuePair>();
            postData.add(new BasicNameValuePair("deviceUniqueId", UniqueIdUtility.GetUniqueId()));
            postData.add(new BasicNameValuePair("userId", userName));
            postData.add(new BasicNameValuePair("deviceName", deviceName));

            String returnValue = HttpUtility.GetHttpResponse(ResourceUtility.GetString(R.string.BindDeviceAPI), postData);
            if (returnValue.equals("1"))
            {
                this.runOnUiThread(new Runnable()
                {
                    public void run()
                    {
                        new AlertDialog.Builder(MainActivity.this)
                                .setMessage(MessageFormat.format(ResourceUtility.GetString(R.string.UserNameNotExist), userName))
                                .setPositiveButton(android.R.string.yes, new DialogInterface.OnClickListener()
                                {
                                    public void onClick(DialogInterface dialog, int which)
                                    {
                                    }
                                })
                                .show();
                    }
                });
            }
            else if (returnValue.equals("2"))
            {
                this.runOnUiThread(new Runnable()
                {
                    public void run()
                    {
                        new AlertDialog.Builder(MainActivity.this)
                                .setMessage(MessageFormat.format(ResourceUtility.GetString(R.string.DeviceNameExisted), deviceName))
                                .setPositiveButton(android.R.string.yes, new DialogInterface.OnClickListener()
                                {
                                    public void onClick(DialogInterface dialog, int which)
                                    {
                                    }
                                })
                                .show();
                    }
                });
            }
        }
        catch (Exception ex)
        {

        }

        CheckBinding();
    }
}
