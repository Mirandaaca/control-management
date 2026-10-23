#!/usr/bin/env bash
# Sube un sitio ya compilado al contenedor $web de una cuenta de Storage y borra lo que ya no existe. Entra con la
# identidad de GitHub Actions (--auth-mode login), sin las claves de la cuenta.
#
#   subir-sitio-estatico.sh <cuenta> <carpeta> [subcarpeta con hash]
#
# Los archivos de la subcarpeta con hash en el nombre (assets/ del panel) cambian de nombre cuando cambian: van con caché
# de un año. Todo lo demás (index.html, y la app web de Flutter, que no lleva hash) va con no-cache: el navegador pregunta
# cada vez si cambió (un 304 si no), y así Julio ve cada versión apenas se despliega, sin forzar la recarga.
set -euo pipefail

cuenta="$1"
carpeta="$2"
con_hash="${3:-}"
comun=(--auth-mode login --account-name "$cuenta" --only-show-errors)

# Primero los archivos con hash, para que index.html nunca apunte a uno que todavía no está.
if [[ -n "$con_hash" ]]; then
  az storage blob upload-batch "${comun[@]}" -d '$web' -s "$carpeta/$con_hash" --destination-path "$con_hash" \
    --overwrite --content-cache-control 'public, max-age=31536000, immutable' >/dev/null
fi

resto="$(mktemp -d)"
rsync -a ${con_hash:+--exclude "/$con_hash"} "$carpeta/" "$resto/"
az storage blob upload-batch "${comun[@]}" -d '$web' -s "$resto" --overwrite --content-cache-control 'no-cache' >/dev/null

# Lo que está en $web y ya no está en la carpeta.
(cd "$carpeta" && find . -type f | sed 's#^\./##' | sort) > "$resto.locales"
az storage blob list "${comun[@]}" -c '$web' --num-results '*' --query '[].name' -o tsv | sort > "$resto.remotos"
comm -23 "$resto.remotos" "$resto.locales" | while read -r sobra; do
  az storage blob delete "${comun[@]}" -c '$web' -n "$sobra" >/dev/null
  echo "Borrado: $sobra"
done
echo "Listo: $(wc -l < "$resto.locales") archivos en $cuenta."
