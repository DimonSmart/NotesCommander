var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.NotesCommander>("notescommander");

builder.Build().Run();
