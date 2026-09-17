#!/bin/sh
# Backup do banco, uma vez por dia.
#
# Formato custom (-Fc) e nao SQL puro: ja sai comprimido e o pg_restore
# consegue restaurar uma tabela sozinha, sem derrubar o resto.
#
# Este backup mora no mesmo servidor, entao ele cobre engano humano — DROP
# errado, migracao ruim, dado apagado sem querer. Nao cobre o servidor
# morrer. Para isso alguem precisa copiar /backups para fora da maquina; o
# README explica como.

set -eu

DIAS_PARA_GUARDAR="${DIAS_PARA_GUARDAR:-14}"
DESTINO=/backups

mkdir -p "$DESTINO"

registra() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

registra "Rotina de backup ativa. Guardando $DIAS_PARA_GUARDAR dias em $DESTINO."

while true; do
    ARQUIVO="$DESTINO/convivium-$(date +%Y%m%d-%H%M%S).dump"

    # Escreve com outro nome e so renomeia no fim: assim nao existe arquivo
    # pela metade em $DESTINO se o processo morrer no meio.
    if pg_dump -Fc -f "$ARQUIVO.parcial" 2>/tmp/erro; then
        mv "$ARQUIVO.parcial" "$ARQUIVO"
        registra "Backup gravado: $(basename "$ARQUIVO") ($(du -h "$ARQUIVO" | cut -f1))"

        # Limpa os antigos so depois de um backup dar certo. Apagar antes
        # deixaria o condominio sem copia nenhuma num dia em que o dump falha.
        find "$DESTINO" -name 'convivium-*.dump' -type f \
            -mtime "+$DIAS_PARA_GUARDAR" -delete
    else
        rm -f "$ARQUIVO.parcial"
        registra "FALHA no backup: $(cat /tmp/erro)"
    fi

    sleep 86400
done
