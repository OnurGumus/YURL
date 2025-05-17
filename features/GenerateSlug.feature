Feature: URL Shortening with Semantic Slugs

    Background:
        Given the URL store is empty

    Scenario: Generate a semantic slug for an HTML page
        Given no URL "https://openai.com/blog" has been shortened yet
        And the page at "https://openai.com/blog" returns HTML with title "OpenAI Blog"
        When I shorten the URL "https://openai.com/blog"
        Then the system should create the slug *
        And navigating to "/openai-blog" should redirect to "https://openai.com/blog"

    # Scenario: Fallback to URL-based slug for non-HTML content
    #     Given no URL "https://api.example.com/v1/data" has been shortened yet
    #     And the page at "https://api.example.com/v1/data" returns content-type "application/json"
    #     When I shorten the URL "https://api.example.com/v1/data"
    #     Then the system should create a slug derived from the URL, for example "api-example-com-v1-data"
    #     And navigating to "/api-example-com-v1-data" should redirect (301) to "https://api.example.com/v1/data"

    # Scenario: Return existing slug without re-fetch
    #     Given the store contains
    #         | url                       | slug          |
    #         | "https://openai.com/blog" | "openai-blog" |
    #     When I shorten "https://openai.com/blog" again
    #     Then I should get back the slug "openai-blog"
    #     And no additional page fetch should occur

    # Scenario: Handle slug collisions
    #     Given the store contains
    #         | url                     | slug        |
    #         | "https://firstsite.com" | "firstsite" |
    #     And our HTML-title logic would also generate "firstsite" for "https://secondsite.com"
    #     When I shorten "https://secondsite.com"
    #     Then the system should generate a unique slug (e.g. "firstsite-1")
    #     And the store should now contain both mappings

    # Scenario: Reject an invalid URL
    #     Given no URLs have been shortened yet
    #     When I shorten the URL "not-a-url"
    #     Then I should see an error "Invalid URL"
    #     And nothing should be added to the store
