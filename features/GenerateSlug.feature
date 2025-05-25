Feature: URL Shortening with Semantic Slugs

    Background:
        Given the URL store is empty

    Scenario: Generate a semantic slug for an HTML page
        Given no URL "https://yurl.ai" has been shortened yet
        And the page at "https://yurl.ai" returns HTML with title "OpenAI Blog"
        When I shorten the URL "https://yurl.ai"
        Then the system should create the slug "ChXtKflH"
        And navigating to "/ChXtKflH" should redirect to "https://yurl.ai/"

    