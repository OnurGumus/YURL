module Model
open FCQRS.Model.Data
open FCQRS.Model.Validation

type Url =
    private 
    |Url of string
    static member Value_ =
        (fun (Url s) -> s),
        (fun (s: string) _ ->
            single (fun t ->
                t.TestOne s
                |> t.NotBlank EmptyString
                |> t.MaxLen 32768 TooLongString
                |> t.Map Url
                |> t.End))

    member this.IsValid = ValueLens.IsValidValue this
    override this.ToString() = (ValueLens.Value this).ToString()


type Slug = 
    private Slug of Url

    with static member Value_ =
            (fun (Slug s) -> s),
            (fun (s: Url) _ -> Slug s )

            member this.IsValid = 
                    ValueLens.IsValidValue (this |> ValueLens.Value)
            override this.ToString() = (ValueLens.Value this).ToString()