var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.NotesCommander_Backend>("notes-backend");
// MAUI project cannot be added to AppHost
// builder.AddProject<Projects.NotesCommander>("notescommander")
//     .WithReference(backend);

builder.Build().Run();
