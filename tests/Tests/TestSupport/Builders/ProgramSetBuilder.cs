using PodBridge.Logic.EpisodeSourcing;

namespace Tests.TestSupport.Builders;

internal sealed class ProgramSetBuilder
{
    private string _title = string.Empty;
    private string? _synopsis;
    private string? _description;
    private string? _sharingUrl;
    private ImageType? _image;
    private PublicationService? _publicationService;
    private List<Item> _items = [];
    private bool _hasNextPage;
    private string? _endCursor;

    public ProgramSetBuilder WithDefaults()
    {
        _title = "Fixture Show";
        _synopsis = "Fixture show synopsis";
        _description = "Fixture show description";
        _sharingUrl = "https://fixture.test/show";
        _image = new ImageType { Url = "https://fixture.test/images/{width}/show.jpg" };
        _publicationService = new PublicationService { Title = "Fixture Publisher" };
        _items = [];
        _hasNextPage = false;
        _endCursor = null;
        return this;
    }

    public ProgramSetBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public ProgramSetBuilder WithItems(params Item[] items)
    {
        _items = items.ToList();
        return this;
    }

    public ProgramSetBuilder WithPagination(bool hasNextPage, string? endCursor)
    {
        _hasNextPage = hasNextPage;
        _endCursor = endCursor;
        return this;
    }

    public ProgramSet Build()
    {
        return new ProgramSet
        {
            Title = _title,
            Synopsis = _synopsis,
            Description = _description,
            SharingUrl = _sharingUrl,
            Image = _image,
            PublicationService = _publicationService,
            Items = new ItemsConnection
            {
                Nodes = _items,
                PageInfo = new PageInfo
                {
                    HasNextPage = _hasNextPage,
                    EndCursor = _endCursor,
                },
            },
        };
    }
}
