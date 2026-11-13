package com.kidssafe.push;

import android.content.Context;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.os.Build;

import java.security.MessageDigest;

/**
 * 读取本安装包的包名与签名证书 SHA-256。
 *
 * 用途：荣耀推送返回 10202 时，成因只有两种 —— 包名不一致，或证书指纹不一致。
 * 让用户在「诊断日志」页直接看到这两个值，就能和开发者服务平台逐字核对，
 * 不必再靠电脑反编译 APK。
 */
public final class AppSignature {

    private AppSignature() {
    }

    /**
     * @return "包名|SHA-256:XX:XX:..." ；出错返回 "包名|error:..."
     */
    public static String get(Context context) {
        String pkg = "";
        try {
            pkg = context.getPackageName();
            PackageManager pm = context.getPackageManager();

            Signature[] sigs;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                PackageInfo pi = pm.getPackageInfo(pkg, PackageManager.GET_SIGNING_CERTIFICATES);
                sigs = pi == null || pi.signingInfo == null
                        ? null
                        : pi.signingInfo.getApkContentsSigners();
            } else {
                PackageInfo pi = pm.getPackageInfo(pkg, PackageManager.GET_SIGNATURES);
                sigs = pi == null ? null : pi.signatures;
            }

            if (sigs == null || sigs.length == 0) {
                return pkg + "|(取不到签名信息)";
            }

            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] digest = md.digest(sigs[0].toByteArray());

            StringBuilder sb = new StringBuilder(digest.length * 3);
            for (byte b : digest) {
                if (sb.length() > 0) {
                    sb.append(':');
                }
                sb.append(String.format("%02X", b));
            }
            return pkg + "|" + sb;
        } catch (Throwable t) {
            return pkg + "|error:" + t;
        }
    }
}
