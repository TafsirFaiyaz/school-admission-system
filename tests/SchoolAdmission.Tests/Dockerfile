FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /tests
COPY . ./
RUN dotnet restore
ENTRYPOINT ["dotnet", "test", "--no-restore", "--logger", "console;verbosity=normal"]
