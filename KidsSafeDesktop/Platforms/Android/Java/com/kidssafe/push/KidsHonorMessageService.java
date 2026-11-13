package com.kidssafe.push;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import com.hihonor.push.sdk.HonorMessageService;
import com.hihonor.push.sdk.HonorPushDataMsg;

/**
 * 接收荣耀推送消息的服务。
 *
 * 荣耀 SDK 只提供了抽象基类 HonorMessageService，**必须由应用自己继承并在
 * AndroidManifest.xml 里声明**（intent-filter: com.hihonor.push.action.MESSAGING_EVENT），
 * 否则 SDK 找不到接收者，token 能拿到但消息收不到。
 *
 * 这里只做「落盘」，不直接弹通知：
 *  Android 13+ 弹通知需要 POST_NOTIFICATIONS 运行时权限，而这是一个儿童桌面，
 *  不该在孩子界面弹权限框。消息先存进 SharedPreferences，由 C# 侧决定怎么用。
 */
public class KidsHonorMessageService extends HonorMessageService {

    private static final String TAG = "KidsHonorPush";

    private static void put(Context ctx, String key, String value) {
        try {
            SharedPreferences p = ctx.getApplicationContext()
                    .getSharedPreferences(HonorPushBridge.PREFS, Context.MODE_PRIVATE);
            p.edit().putString(key, value).apply();
        } catch (Throwable t) {
            Log.w(TAG, "save " + key + " failed", t);
        }
    }

    /** token 发生变更（首次注册、或服务端要求刷新）。 */
    @Override
    public void onNewToken(String token) {
        super.onNewToken(token);
        put(this, HonorPushBridge.KEY_TOKEN, token == null ? "" : token);
        Log.i(TAG, "onNewToken");
    }

    /** 收到透传消息（data message）。 */
    @Override
    public void onMessageReceived(HonorPushDataMsg msg) {
        super.onMessageReceived(msg);
        if (msg == null) {
            return;
        }
        String data = msg.getData();
        put(this, HonorPushBridge.KEY_LAST_MSG, data == null ? "" : data);
        put(this, HonorPushBridge.KEY_LAST_MSG_AT, String.valueOf(System.currentTimeMillis()));
        Log.i(TAG, "onMessageReceived type=" + msg.getType() + " len="
                + (data == null ? 0 : data.length()));
    }
}
