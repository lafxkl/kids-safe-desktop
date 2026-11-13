package com.kidssafe.push;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Log;

import com.hihonor.push.sdk.HonorPushCallback;
import com.hihonor.push.sdk.HonorPushClient;

/**
 * 荣耀推送（Push Kit）与 C# 之间的唯一桥梁。
 *
 * 为什么要有这个类：.NET for Android 只能直接调用「已经生成绑定」的 Java 类，
 * 荣耀推送 SDK 没有官方 C# 绑定，所以这里用 Java 写一层极薄的静态方法，
 * 由 .NET 侧通过 JNI 调用（见 Platforms/Android/HonorPush.cs）。
 *
 * 设计原则（重要）：
 *  1. 所有方法都吞掉异常、绝不向上抛——推送只是增强功能，任何机型/任何失败
 *     都不允许影响儿童桌面的启动。
 *  2. 结果不通过回调回传 C#（跨线程 JNI 回调容易踩坑），而是写进
 *     SharedPreferences("honor_push")，C# 侧自己去读，简单且不会漏线程问题。
 */
public final class HonorPushBridge {

    private static final String TAG = "KidsHonorPush";
    public static final String PREFS = "honor_push";

    public static final String KEY_TOKEN = "token";        // 推送 token
    public static final String KEY_SUPPORT = "support";    // 本机是否支持荣耀推送
    public static final String KEY_DIAG = "diag";          // 最近一次失败原因
    public static final String KEY_LAST_MSG = "last_msg";  // 最近一条透传消息
    public static final String KEY_LAST_MSG_AT = "last_msg_at";

    private HonorPushBridge() {
    }

    private static SharedPreferences prefs(Context ctx) {
        return ctx.getApplicationContext()
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    private static void put(Context ctx, String key, String value) {
        try {
            prefs(ctx).edit().putString(key, value).apply();
        } catch (Throwable ignored) {
        }
    }

    /**
     * 初始化并申请 token。由 C# 在 Activity 创建后调用一次。
     *
     * @return "ok" 已发起申请 / "unsupported" 非荣耀设备 / "error:..." 异常
     */
    public static String start(Context context) {
        if (context == null) {
            return "error:no-context";
        }
        try {
            final Context app = context.getApplicationContext();

            boolean support;
            try {
                support = HonorPushClient.getInstance().checkSupportHonorPush(app);
            } catch (Throwable t) {
                // 部分机型上 checkSupportHonorPush 自身会抛，此时按「不支持」处理
                put(app, KEY_SUPPORT, "false");
                put(app, KEY_DIAG, "checkSupport:" + t);
                return "unsupported";
            }

            // 注意：必须存字符串。C# 侧统一用 getString 读，存 boolean 会抛 ClassCastException
            //（被 catch 吞掉 → 读回 null → 误报「不支持」，这个 bug 在 v1.7 真机日志里暴露过）。
            put(app, KEY_SUPPORT, support ? "true" : "false");
            if (!support) {
                return "unsupported";
            }

            HonorPushClient.getInstance().init(app, false);

            // 有缓存 token 会立刻回调 onSuccess，没有则向服务端申请
            HonorPushClient.getInstance().getPushToken(new HonorPushCallback<String>() {
                @Override
                public void onSuccess(String token) {
                    put(app, KEY_TOKEN, token == null ? "" : token);
                    put(app, KEY_DIAG, "");
                    Log.i(TAG, "getPushToken success");
                }

                @Override
                public void onFailure(int errorCode, String errorMsg) {
                    put(app, KEY_DIAG, "token:" + errorCode + ":" + errorMsg);
                    Log.w(TAG, "getPushToken failed " + errorCode + " " + errorMsg);
                }
            });

            return "ok";
        } catch (Throwable t) {
            Log.e(TAG, "start failed", t);
            try {
                put(context, KEY_DIAG, "start:" + t);
            } catch (Throwable ignored) {
            }
            return "error:" + t;
        }
    }

    /** 家长退出登录/关闭推送时解绑，避免设备继续收消息。 */
    public static void deleteToken(Context context) {
        if (context == null) {
            return;
        }
        try {
            final Context app = context.getApplicationContext();
            HonorPushClient.getInstance().deletePushToken(new HonorPushCallback<Void>() {
                @Override
                public void onSuccess(Void unused) {
                    prefs(app).edit().remove(KEY_TOKEN).apply();
                }

                @Override
                public void onFailure(int errorCode, String errorMsg) {
                    put(app, KEY_DIAG, "delete:" + errorCode + ":" + errorMsg);
                }
            });
        } catch (Throwable t) {
            Log.e(TAG, "deleteToken failed", t);
        }
    }
}
