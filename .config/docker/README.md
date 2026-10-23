# Docker

| 文件 | 镜像 |
|------|------|
| `Dockerfile.dev` | `ew4-dev:local` — SDK + Python + Node，供 `scripts/ew4.sh` |
| `Dockerfile` | `ew4-web:local` — 发布 `Ew4.Backend.Web` |

```bash
docker compose -f .config/docker/compose.yaml build dev
# 或
scripts/ew4.sh image dev
```

构建 context 为**仓库根**；`.dockerignore` 在根目录。
