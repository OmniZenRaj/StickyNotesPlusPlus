using System.Collections.Generic;

namespace OmniZenNotes.Models;

class Notebook : Entity
{
    public List<Note> Notes { get; set; } = new();

    public string DbPathUri { get; set; }

    public Notebook(string dbPathUri) {
        DbPathUri = dbPathUri;
    }
}
