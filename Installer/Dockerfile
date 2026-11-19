# Toolchain for packaging the Windows installer from macOS or Linux: NSIS (makensis) + ImageMagick for the wizard art.
FROM debian:bookworm-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends nsis imagemagick fonts-dejavu-core curl ca-certificates unzip \
 && rm -rf /var/lib/apt/lists/*

# Debian's makensis is fine, but its installer stubs and plugins are MinGW builds: an installer made with them got
# "This app can't run on your PC" on a tester's Windows machine, although Wine ran it. Use the official
# Windows-built stubs and plugins from the NSIS release of the same version, as installers built on Windows do.
ARG NSIS_ZIP_SHA256=1bb9fc85ee5b220d3869325dbb9d191dfe6537070f641c30fbb275c97051fd0c
RUN test "$(makensis -VERSION)" = "v3.08-3+deb12u1" \
 && curl -fsSL -o /tmp/nsis.zip "https://downloads.sourceforge.net/project/nsis/NSIS%203/3.08/nsis-3.08.zip" \
 && echo "$NSIS_ZIP_SHA256  /tmp/nsis.zip" | sha256sum -c - \
 && unzip -q /tmp/nsis.zip -d /tmp \
 && cp -r /tmp/nsis-3.08/Stubs/. /usr/share/nsis/Stubs/ \
 && cp -r /tmp/nsis-3.08/Plugins/x86-unicode/. /usr/share/nsis/Plugins/x86-unicode/ \
 && rm -rf /tmp/nsis.zip /tmp/nsis-3.08

# Lilita One (SIL Open Font License), the rounded display face of the installer's "BABY CLASH" lettering.
RUN mkdir -p /usr/share/fonts/truetype/lilita \
 && curl -fsSL -o /usr/share/fonts/truetype/lilita/LilitaOne-Regular.ttf \
    https://github.com/google/fonts/raw/main/ofl/lilitaone/LilitaOne-Regular.ttf

WORKDIR /project
