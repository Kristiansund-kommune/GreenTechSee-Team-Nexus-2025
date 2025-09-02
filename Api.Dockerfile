FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt-get update && apt-get install -y ffmpeg wget ca-certificates && rm -rf /var/lib/apt/lists/*
# install piper (static binary)
RUN wget -O /usr/local/bin/piper https://github.com/rhasspy/piper/releases/latest/download/piper_linux_x86_64 \
 && chmod +x /usr/local/bin/piper

WORKDIR /app
COPY ./publish/ .  # dotnet publish output

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "YourApi.dll"]
