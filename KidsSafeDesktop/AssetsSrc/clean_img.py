import sys
from PIL import Image
import numpy as np
from collections import deque

def clean(path, out_path=None, tol=60, small_frac=0.02):
    im = Image.open(path).convert('RGBA')
    arr = np.array(im)
    rgb = arr[..., :3].astype(int)
    H, W = arr.shape[:2]
    # background color from the outermost border RING (robust for centered objects)
    rw = 14
    ring = np.vstack([
        rgb[:rw, :, :].reshape(-1, 3),
        rgb[-rw:, :, :].reshape(-1, 3),
        rgb[:, :rw, :].reshape(-1, 3),
        rgb[:, -rw:, :].reshape(-1, 3),
    ])
    bg = np.median(ring, axis=0).astype(int)
    # distance to bg
    d = np.sqrt(((rgb - bg) ** 2).sum(axis=2))
    mask = d < tol  # background pixels
    # flood fill from border over mask
    visited = np.zeros((H, W), bool)
    dq = deque()
    for x in range(W):
        for y in (0, H - 1):
            if mask[y, x]:
                dq.append((y, x)); visited[y, x] = True
    for y in range(H):
        for x in (0, W - 1):
            if mask[y, x] and not visited[y, x]:
                dq.append((y, x)); visited[y, x] = True
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not visited[ny, nx]:
                visited[ny, nx] = True
                dq.append((ny, nx))
    arr[visited, 3] = 0
    # remove tiny connected components (watermark specks), keep largest
    opaque = arr[..., 3] > 20
    # label connected components
    lab = np.zeros((H, W), np.int32)
    cur = 0
    sizes = {}
    for y in range(H):
        for x in range(W):
            if opaque[y, x] and lab[y, x] == 0:
                cur += 1
                dq = deque([(y, x)]); lab[y, x] = cur; cnt = 0
                while dq:
                    yy, xx = dq.popleft(); cnt += 1
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = yy + dy, xx + dx
                        if 0 <= ny < H and 0 <= nx < W and opaque[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = cur; dq.append((ny, nx))
                sizes[cur] = cnt
    if sizes:
        biggest = max(sizes, key=sizes.get)
        thresh = small_frac * H * W
        for comp, c in sizes.items():
            if comp != biggest and c < thresh:
                arr[lab == comp, 3] = 0
    out = Image.fromarray(arr, 'RGBA')
    out.save(out_path or path)
    tp = (arr[..., 3] < 10).mean() * 100
    print(f'{out_path or path}: bg~{bg.tolist()} transparent={tp:.1f}%')

if __name__ == '__main__':
    clean(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
