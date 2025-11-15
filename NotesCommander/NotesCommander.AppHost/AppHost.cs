var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NotesCommanderBackend>("notes-backend");
builder.AddProject<Projects.NotesCommander>("notescommander")
    .WithReference(backend);

builder.Build().Run();
